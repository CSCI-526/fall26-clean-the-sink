using UnityEngine;
namespace SinkLab
{
    public sealed class WaterVisuals:MonoBehaviour
    {
        public WaterJet water;
        public Transform hoseAnchor;
        public Material waterMaterial,hoseMaterial;
        [Header("Drawn thickness. Stream 0 is the focused jet. Streams 1-6 appear only in wide spray.")]
        public float focusedStartWidth=.034f,focusedEndWidth=.05f,wideStartWidth=.016f,wideEndWidth=.028f;
        LineRenderer[] streams;
        LineRenderer hose;
        ParticleSystem splash;
        AudioSource flow;
        float emitRemainder;
        void Start()
        {
            streams=new LineRenderer[7];for(int i=0;i<streams.Length;i++)streams[i]=Line("Water stream "+i,waterMaterial,.013f,3);
            hose=Line("Flexible faucet hose",hoseMaterial,.035f,24);
            var go=new GameObject("Water splash droplets");go.transform.SetParent(transform);splash=go.AddComponent<ParticleSystem>();
            var main=splash.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.24f;main.startSpeed=0;main.startSize=.027f;main.gravityModifier=.8f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=500;
            var emission=splash.emission;emission.enabled=false;var shape=splash.shape;shape.enabled=false;splash.GetComponent<ParticleSystemRenderer>().sharedMaterial=waterMaterial;splash.Play();
            flow=gameObject.AddComponent<AudioSource>();flow.loop=true;flow.playOnAwake=false;flow.volume=0;flow.spatialBlend=0;
            var clip=AudioClip.Create("Procedural running water",24000,1,24000,false);var data=new float[24000];var random=new System.Random(178);float low=0;
            for(int i=0;i<data.Length;i++){float noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.32f);data[i]=low*.38f;}clip.SetData(data,0);flow.clip=clip;flow.Play();
        }
        LineRenderer Line(string n,Material m,float width,int count)
        {var go=new GameObject(n);go.transform.SetParent(transform);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=m;l.startWidth=width;l.endWidth=width;l.positionCount=count;l.useWorldSpace=true;l.numCapVertices=3;return l;}
        void LateUpdate()
        {
            if(streams==null)return;
            if(!water || !water.aimCamera)
            {
                foreach(var stream in streams)if(stream)stream.enabled=false;
                if(hose)hose.enabled=false;
                if(flow)flow.volume=0;
                return;
            }
            Vector3 from=water.nozzle?water.nozzle.position:water.aimCamera.transform.position;
            Vector3 to=water.HasHit?water.LastHitPoint:from+water.aimCamera.transform.forward*3;
            var axis=(to-from).normalized;var side=Vector3.Cross(axis,Vector3.up).normalized;var up=Vector3.Cross(side,axis);
            for(int i=0;i<streams.Length;i++)
            {
                var l=streams[i];l.enabled=water.IsSpraying&&(i==0||water.WideSpray);if(!l.enabled)continue;
                float a=i*Mathf.PI*2/6;var spread=i==0?Vector3.zero:(side*Mathf.Cos(a)+up*Mathf.Sin(a))*water.EffectiveRadius*.65f;
                float punch=Mathf.Lerp(.7f,1.45f,water.Pressure);
                l.startWidth=(water.WideSpray?wideStartWidth:focusedStartWidth)*punch;l.endWidth=(water.WideSpray?wideEndWidth:focusedEndWidth)*punch;
                l.SetPosition(0,from);l.SetPosition(1,Vector3.Lerp(from,to+spread,.5f)+side*Mathf.Sin(Time.time*38+i)*.006f);l.SetPosition(2,to+spread);
            }
            hose.enabled=hoseAnchor!=null;
            if(hoseAnchor)
                for(int i=0;i<24;i++){float t=i/23f;hose.SetPosition(i,Vector3.Lerp(hoseAnchor.position,from,t)+Vector3.down*Mathf.Sin(t*Mathf.PI)*.26f);}
            if(water.IsSpraying&&water.HasHit)
            {
                emitRemainder+=Time.deltaTime*110;int n=Mathf.FloorToInt(emitRemainder);emitRemainder-=n;
                for(int i=0;i<n;i++){var e=new ParticleSystem.EmitParams();e.position=to+water.LastHitNormal*.025f+Random.insideUnitSphere*.025f;e.velocity=(water.LastHitNormal+Random.insideUnitSphere*.9f)*Random.Range(.4f,1.4f);e.startSize=Random.Range(.009f,.03f);e.startColor=new Color(.7f,.9f,1,.7f);splash.Emit(e,1);}
            }
            flow.volume=Mathf.MoveTowards(flow.volume,water.IsSpraying?.12f+water.Pressure*.09f:0,Time.unscaledDeltaTime*.8f);flow.pitch=water.WideSpray?1.15f:.88f+water.Pressure*.15f;
        }
        void OnDestroy(){if(flow&&flow.clip)Destroy(flow.clip);}
    }
}
