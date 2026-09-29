# Paired Prototype Descriptive Document

## Project Name: Clean it up!

### Team Members

- **Name**:  | **Email**:  | **GitHub Username**: 
- **Name**: [Teammate Name] | **Email**: [Teammate Email] | **GitHub Username**: [Teammate GitHub]

---

### Logline (Genre + Dual Twists + Fail State)

**(Physics-Based Cleaning Simulation + Constricting Drain & Overflow Game Over)**
*Sink Havoc: Choked Drain & Overflow* is a first-person physics cleaning simulation. The player walks around the sink and aims a pull-out sprayer to push food into the drain and wash stains. Spraying adds water, and higher pressure fills the basin faster. The hole shrinks on its own and shrinks again each time it swallows a scrap, until it seals. Two losses end a run that is still dirty: the drain seals completely, or the water crosses the rim. The player can also plug the hole so food floats and drifts into a new layout, but a plugged basin fills quickly and spills if it stays closed too long.

---



### Genre Research & Twists



#### 1. Three Researched Games in the Genre:

1. *PowerWash Simulator*
2. *Viscera Cleanup Detail*
3. *Fresh Start Cleaning Simulator*



#### 2. Genre Tropes:

- **Absence of Hard Failure States**: Cleaning simulations prioritize zen-like, meditative relaxation where mistakes never trigger sudden loss conditions or penalties.
- **Cost-Free Continuous Spray**: Players hold down the trigger indefinitely without resource limits, fluid build-up, or detrimental environmental reactions.
- **Invariant Disposal Capacity**: Drains and incinerators maintain infinite throughput and fixed dimensions, allowing arbitrary cleanup sequences.
- **Predictable 2D Ground Sliding**: Debris remains grounded on a flat surface, driven exclusively by 2D sliding friction and horizontal momentum.



#### 3. Core Dual Twists & Critical Fail State:

1. **Twist A: Constricting Drainage Hole (Dynamic Radius)**:

- The circular drain shrinks over time and permanently tightens each time a scrap is swallowed. Outflow follows the area of the opening. A full hole still drains faster than the spray, so the water stays down until the hole has actually narrowed.
- At its minimum the opening seals. A sealed drain while food or stains remain is a loss. Once per sink, the player can snap the hole fully open; it then shrinks back to the size it had before that press, at 40 times the normal speed. Resetting the sink restores that one use.
- Plugging the hole is separate from this shrink. A plug stops outflow until the player opens it again. A dry open hole does not pull. Standing water over an open hole forms a vortex that draws nearby submerged food in.

2. **Twist B: Liquid Accumulation & Buoyancy**:

- Spraying pumps water into the basin. Higher pressure fills it faster and pushes harder. The wide shower covers more area, pushes less, and fills faster than the focused jet.
- While the level is rising, submerged food floats and drifts into a new layout. Plugging the drain is the deliberate way to do this: held water can shake a stuck scrap loose, and opening the hole again lets that water carry food toward the drain.
- Deep water weakens the jet before it reaches the floor, so the same spray pushes less and scrubs stains more slowly. Over an open hole, buoyancy eases off so a scrap already above the mouth can fall in.

3. **Core Fail States (Instant Loss)**:

- **Overflow:** if the water crosses the rim, the sink spills and the run ends immediately.
- **Sealed drain:** if the hole closes completely and any food or stain is still left, the run ends immediately.
- Clearing every scrap and every stain is a win. These losses apply only while the sink is still dirty.



#### 4. How the Twists Innovate the Genre:

1. **Introducing High-Stakes Urgency**: Endless spraying is no longer safe. The player is racing a hole that will seal, and any water held in the basin can spill over the rim.
2. **Cascading Failure Spiral**: Shrinking drain -> slower outflow -> the same spray can finally raise the water -> floating scraps drift off the hole -> the rim gets closer, and every swallowed scrap also brings the seal closer. A plugged hole skips straight to the rise.
3. **Trigger Discipline & a Chosen Flood**: Early on, a full hole outruns the spray, so the player does not have to stop just to keep the basin empty. After the hole narrows, pauses matter again. Plugging is the voluntary version of that risk: raise the water so stuck food floats and shifts, then open the hole before it spills. Higher pressure and the wide shower buy stronger or broader cleaning at the cost of a faster fill.

---



### Short Prototype Description

*Sink Havoc: Choked Drain & Overflow* is a 3D first-person cleaning prototype. WASD walks the player around the counter, mouse-look aims a pull-out sprayer, and holding the left mouse button sprays. The mouse wheel sets pressure. The objective is to push every scrap into the circular drain and wash every stain. The hole constricts over time and with every scrap it swallows; outflow drops with its area, and a fully sealed hole ends the run if anything remains. Spraying adds water, but a wide-open hole still empties faster than the jet, so the basin stays low until the hole has shrunk or the player plugs it. A plug stops the drain, the level rises, and floating scraps drift into a new arrangement. That reshuffle can free a stuck piece, and standing water over the opened hole pulls nearby food in. A dry hole does not pull. Deep water weakens the jet and slows stain scrubbing. Water over the rim is an instant loss, so a plug has to be opened again before the shallow basin spills.

---



### Twist & Mechanics Matrix


| Mechanic                        | Description                                                                                | Interaction with Dual Twists & Fail State                                                                                                             | Affected Genre Elements                                                | Type of Genre Innovation                            |
| ------------------------------- | ------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | --------------------------------------------------- |
| **High-Pressure Jet Spraying**  | Hold the left mouse button to push food downstream and scrub stains. The wheel raises pressure. The player can switch between a focused jet and a wide shower. | Higher pressure pushes harder and fills faster. The wide shower covers more, pushes less, and fills faster still. Deep water weakens the jet. **Water over the rim is an immediate loss.** | Lack of failure penalties; unlimited spraying trope; low-stress pacing | **Fatal Environmental Hazard & Risk-Reward Pacing** |
| **Prioritized Debris Flushing** | Aim the spray to move scraps into the circular drain. Plug the hole to hold water, or open it so standing water can pull food in. Once per sink, the hole can be snapped fully open and then shrinks back at 40 times the normal speed. | The hole shrinks over time and with every swallowed scrap. A full hole still outruns the spray. A plug stops outflow so food floats and drifts into a new layout; opening too late spills. **A hole that seals while food or stains remain is a loss.** A dry hole does not pull. | Fixed disposal capacity; arbitrary cleanup sequence                    | **Cascading Hazard & Spatial Prioritization**       |


---



### Deliverable Links

- **GitHub Repository**: `https://github.com/Firemanwolf/clean-the-sink`
- **WebGL Playable Build**: `https://[Your-GitHub-Username].github.io/paired-prototype-sink-havoc/`
- **Gameplay Video**: `[Insert public link to video demo]`

---



### Individual Contributions

---



### Diagram / Sketch Concept

*(Include in your final submission document)*

- **Top/Isometric View**: Show the circular drain shrinking from its starting radius to a sealed minimum. Mark that seal, with food or stains still left, as a loss. Show a plugged hole holding water so scraps drift into a new layout.
- **Side Cutaway View**:
- **Sink Basin Rim**: Annotate the top lip with **"Overflow = Immediate Game Over"**.
- **Water Accumulation**: While the hole is still wide, outflow beats the spray and the level stays down. After the hole narrows, or while it is plugged, the level rises toward the rim. Annotate `Plug to float and reshuffle; open again before the rim`.
- **Floating Debris**: While the level is rising, buoyant scraps drift. Over an open hole with standing water, a vortex pulls nearby food down. A dry hole has no pull.
- **Drain Hole**: Show the narrowed outlet with `Smaller hole = slower outflow`, and the sealed minimum with `Fully closed + still dirty = Game Over`.

