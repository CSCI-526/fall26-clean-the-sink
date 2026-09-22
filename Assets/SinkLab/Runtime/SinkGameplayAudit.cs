#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SinkLab.Editor
{
    // Editor-only observer/controller. Uses the playable movement, aim and water APIs;
    // never relocates food, applies forces, calls Wash or calls MarkDrained.
    public sealed class SinkGameplayAudit:MonoBehaviour
    {
        [Serializable] public class AuditResult {public bool finished,passed;public string status;public int foodRemaining,stainsRemaining;public float seconds;public List<string> observations=new List<string>();}
        public AuditResult result=new AuditResult();
        SinkWorld world;
        readonly Vector3[] stations={new Vector3(0,0,-1.72f),new Vector3(2.4f,0,-1.72f),new Vector3(2.35f,0,0),new Vector3(2.4f,0,1.72f),new Vector3(0,0,1.72f),new Vector3(-2.4f,0,1.72f),new Vector3(-2.35f,0,0),new Vector3(-2.4f,0,-1.72f)};
        int station;
        float started;
        bool routeFailed;
        string output="Verification";
        IEnumerator Start()
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));
            world=FindFirstObjectByType<SinkWorld>();Directory.CreateDirectory(output);
            if(!world){Finish(false,"No playable world found");yield break;}
            world.ResetRun();world.player.enabled=false;world.player.SetControl(true);world.water.Pressure=.8f;started=Time.time;
            Application.runInBackground=true;
            Capture("01-start");yield return new WaitForSeconds(.5f);
            result.status="Washing all six stains through the actual nozzle";Save();
            foreach(var stain in world.stains)
            {
                AlignStations(stain.transform.position);
                int firstStation=StationFor(stain.transform.position);
                for(int attempt=0;attempt<4&&!stain.IsClean;attempt++)
                {
                    yield return RouteTo((firstStation+attempt*2)%8);
                    if(routeFailed){Finish(false,"Player could not navigate around counter");yield break;}
                    world.water.WideSpray=true;float began=Time.time,deadline=Time.time+9;float previous=stain.Remaining;
                    while(!stain.IsClean&&Time.time<deadline)
                    {
                        float mark=Time.time;yield return DrainPause();if(result.finished)yield break;
                        float waited=Time.time-mark;deadline+=waited;began+=waited;
                        Aim(stain.transform.position);world.water.SetSpraying(true);yield return new WaitForFixedUpdate();
                        if(Time.time-began>2&&Mathf.Abs(stain.Remaining-previous)<.001f)break;
                    }
                    world.water.SetSpraying(false);
                }
                world.water.SetSpraying(false);result.observations.Add(stain.name+": "+(stain.IsClean?"washed":"FAILED, remaining "+stain.Remaining));Save();
                if(!stain.IsClean){Capture("failed-stain");Finish(false,"Unreachable stain: "+stain.name);yield break;}
            }
            Capture("02-stains-washed");
            world.water.WideSpray=false;
            foreach(var food in world.foods)
            {
                if(food.IsDrained)continue;
                result.status="Guiding "+food.name+" with water";Save();
                AlignStations(food.transform.position);
                yield return RouteTo(StationFor(food.transform.position));
                float deadline=Time.time+80;
                float lastProgress=Time.time,bestDistance=Vector3.Distance(food.transform.position,world.drain.transform.position);
                int frames=0;
                while(!food.IsDrained&&Time.time<deadline)
                {
                    Vector3 position=food.Body.position;
                    if(position.y<.64f){yield return new WaitForFixedUpdate();continue;}
                    float mark=Time.time;yield return DrainPause();if(result.finished)yield break;
                    float waited=Time.time-mark;deadline+=waited;lastProgress+=waited;
                    Vector3 delta=world.drain.transform.position-position;delta.y=0;
                    Vector3 velocity=food.Body.linearVelocity;velocity.y=0;
                    Vector3 desired=(delta.normalized-velocity*.65f).normalized;
                    bool overOpening=Mathf.Abs(position.x)<.13f&&Mathf.Abs(position.z-.25f)<.13f;
                    float score=ChooseAim(food,desired);
                    if(delta.magnitude<bestDistance-.045f){bestDistance=delta.magnitude;lastProgress=Time.time;}
                    if(!overOpening&&(score<.25f||Time.time-lastProgress>5))
                    {
                        world.water.SetSpraying(false);AlignStations(position);yield return RouteTo((station+2)%8);lastProgress=Time.time;continue;
                    }
                    world.water.Pressure=world.water.WideSpray?1f:(delta.magnitude<.45f?.60f:.85f);
                    world.water.SetSpraying(!overOpening);
                    if(frames++==60)Capture("03-guiding-food");
                    yield return new WaitForFixedUpdate();
                }
                world.water.SetSpraying(false);result.observations.Add(food.name+": "+(food.IsDrained?"physically drained":"FAILED at "+food.Body.position.ToString("F3")));Save();
                if(!food.IsDrained){Capture("failed-food");Finish(false,"Could not guide "+food.name+" to drain");yield break;}
            }
            yield return RouteTo(0);Aim(new Vector3(0,.8f,0));world.water.SetSpraying(false);
            yield return new WaitForSeconds(.25f);Capture("04-complete");
            Finish(world.IsComplete,world.IsComplete?"All stains washed and all food drained using movement, aim and spray only":"Counts did not complete");
        }
        int StationFor(Vector3 point)
        {Vector3 d=point-world.drain.transform.position;return Mathf.Abs(d.x)>Mathf.Abs(d.z)?(d.x>0?2:6):(d.z>0?4:0);}
        void AlignStations(Vector3 target)
        {stations[0].x=stations[4].x=Mathf.Clamp(target.x,-1.2f,1.2f);stations[2].z=stations[6].z=Mathf.Clamp(target.z,-.7f,.7f);}
        float ChooseAim(FoodScrap food,Vector3 desired)
        {
            float best=-2;Vector3 bestPoint=food.Body.position;bool bestWide=false;
            for(int mode=0;mode<2;mode++)for(int i=0;i<12;i++)
            {
                Vector3 direction=Quaternion.Euler(0,i*30,0)*desired;
                Vector3 candidate=food.Body.position-direction*(mode==0?.14f:.24f);candidate.y=.801f;
                Aim(candidate);
                var ray=world.water.aimCamera.ViewportPointToRay(new Vector3(.5f,.5f));RaycastHit eyeHit,hit;
                if(!Physics.Raycast(ray,out eyeHit,8,world.water.waterMask,QueryTriggerInteraction.Ignore))continue;
                Vector3 origin=world.water.nozzle.position,travel=eyeHit.point-origin;
                if(!Physics.Raycast(origin,travel.normalized,out hit,travel.magnitude+.035f,world.water.waterMask,QueryTriggerInteraction.Ignore))continue;
                var direct=hit.collider.GetComponentInParent<FoodScrap>();
                Vector3 push;float coverage=1;
                if(direct==food)push=world.water.GuidePush(travel,food.Body.position-hit.point,food.Body.position,true);
                else
                {
                    if(direct||hit.normal.y<.65f)continue;
                    float radius=mode==0?world.water.focusedRadius:world.water.sprayRadius;
                    float distance=Vector3.Distance(food.ClosestPoint(hit.point),hit.point);
                    if(distance>radius)continue;
                    push=world.water.GuidePush(travel,food.Body.position-hit.point,food.Body.position,false);
                    coverage=Mathf.Lerp(.28f,1,1-distance/radius);
                }
                float score=Vector3.Dot(push,desired)*coverage*(mode==0?1:.8f);
                if(score>best){best=score;bestPoint=candidate;bestWide=mode==1;}
            }
            world.water.WideSpray=bestWide;Aim(bestPoint);return best;
        }
        IEnumerator RouteTo(int destination)
        {
            world.water.SetSpraying(false);
            int forward=(destination-station+8)%8,backward=(station-destination+8)%8;int dir=forward<=backward?1:-1;
            do
            {
                if(station!=destination)station=(station+dir+8)%8;float timeout=Time.time+10;
                while(Time.time<timeout)
                {
                    Vector3 delta=stations[station]-world.player.transform.position;delta.y=0;
                    if(delta.magnitude<.06f)break;
                    var t=world.player.transform;
                    Vector2 input=new Vector2(Vector3.Dot(delta.normalized,t.right),Vector3.Dot(delta.normalized,t.forward));
                    world.player.Move(input,Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }
                if(Time.time>=timeout){routeFailed=true;yield break;}
            }while(station!=destination);
        }
        void Aim(Vector3 point)
        {
            Vector3 direction=point-world.player.viewCamera.transform.position;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float pitch=Mathf.Atan2(-direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            world.player.SetView(yaw,pitch);
        }
        IEnumerator DrainPause()
        {
            BasinWater basin=world!=null?world.basin:null;
            if(basin==null||basin.NormalizedLevel<.68f)yield break;
            if(basin.IsOverflowed){if(!result.finished)Finish(false,"The sink overflowed");yield break;}
            world.water.SetSpraying(false);
            float until=Time.time+22f;
            while(!basin.IsOverflowed&&basin.NormalizedLevel>.3f&&Time.time<until)
                yield return new WaitForFixedUpdate();
            if(basin.IsOverflowed&&!result.finished)Finish(false,"The sink overflowed");
        }
        void Finish(bool passed,string message)
        {
            result.finished=true;result.passed=passed;result.status=message;result.seconds=Time.time-started;
            if(world){world.water.SetSpraying(false);result.foodRemaining=world.FoodRemaining;result.stainsRemaining=world.StainsRemaining;world.player.enabled=true;world.player.SetControl(false);}
            Time.timeScale=1f;
            Save();Debug.Log("Sink gameplay audit: "+message);
        }
        void Save(){if(world){result.foodRemaining=world.FoodRemaining;result.stainsRemaining=world.StainsRemaining;result.seconds=Time.time-started;}File.WriteAllText(output+"/gameplay-audit.json",JsonUtility.ToJson(result,true));}
        void Capture(string name)
        {
            // Camera render is evidence of the actual play scene; HUD screenshots captured separately.
            var camera=world.player.viewCamera;var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG());
            camera.targetTexture=previous;RenderTexture.active=active;Destroy(texture);rt.Release();Destroy(rt);
        }
    }
}
#endif
