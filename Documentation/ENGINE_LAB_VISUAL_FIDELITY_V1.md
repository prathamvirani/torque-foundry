# Engine Lab Visual Fidelity Pass v1

## Purpose

Visual Fidelity Pass v1 replaces the game-facing wall-envelope prototype with a semi-realistic procedural inline-four assembly. Its target is recognizable, mechanically coherent automotive-engine geometry suitable for teaching and inspection. It is not production art, an OEM CAD model, a structural model, or a source of simulation state.

The disabled `InlineFourVisualizer` and `InlineFourEngineContextVisualizer` components remain in the scene only as prototype/reference implementations. `InlineFourVisualFidelityAssembly` is the active presentation.

## Authority boundary

The following values remain authoritative and originate in `EngineLabController` / Core:

- bore
- stroke and crank radius
- connecting-rod length between big-end and small-end centres
- four-cylinder inline layout and established 1/4–2/3 crank phasing
- crank-pin coordinates
- piston-pin coordinates from `SliderCrankKinematics`
- calculated displacement, piston speed, ratios, and clearance volume

The presentation reads those values but never writes derived visual dimensions back to the controller or Core.

## Presentation assumptions

These proportions are visual heuristics expressed in metres after scaling from authoritative geometry. They are named so their purpose is visible and they must not be interpreted as simulated design inputs.

| Visual quantity | v1 assumption | Purpose |
|---|---:|---|
| Cylinder spacing | `1.15 × bore` | Preserves the established I4 prototype spacing |
| Block depth | `1.50 × bore` | Plausible compact water-jacket/crankcase width |
| Crankcase depth below crank axis | `0.72 × bore` | Deep-skirt block and sump clearance |
| Nominal cast wall/rib scale | `0.055 × bore` | Readable casting thickness at game scale |
| Deck thickness scale | `0.045 × bore` | Machined deck and liner support land |
| Head height | `0.82 × bore` | Compact modern DOHC casting envelope |
| Head depth | `1.68 × bore` | Ports, valve angle, and two camshafts |
| Piston diameter | `0.94 × bore` | Visible running clearance without implying a tolerance model |
| Piston compression height | `0.30 × bore` | Pin-to-crown visual proportion |
| Piston skirt length | `0.48 × bore` | Stable modern skirt silhouette |
| Rod big-end outer radius | `0.22 × bore` | Forged big-end and cap silhouette |
| Rod small-end outer radius | `0.105 × bore` | Wrist-pin eye silhouette |
| Rod visual thickness | `0.095 × bore` | I-beam flange/web readability |
| Main-journal visual radius | `0.10 × stroke` | Crankshaft presentation only |
| Rod-journal visual radius | `0.085 × stroke` | Crankshaft presentation only |
| Valve included angle | `20°` | Compact pent-roof DOHC layout |
| Camshaft separation | `0.40 × head depth` | Makes both cams readable in inspection views |
| Camshaft visual radius | `0.075 × bore` | Presentation-only shaft/lobe scale |

Secondary bevels, bosses, ribs, cap bolts, port lands, cover radii, journal lengths, and casting transitions are similarly visual heuristics. They are intentionally reduced-order and do not represent stress, oil-flow, coolant-flow, thermal, sealing, mass, bearing-clearance, or manufacturability calculations.

## Assembly content

Visual Fidelity Pass v1 procedurally builds:

- a stepped cast block with four integrated barrel bays, deck/bore lands, front/rear faces, deep crankcase skirt, five main-bearing bulkheads/saddles and caps, ribs/bosses, sump rail, and shallow oil pan
- five main journals, four rod journals, broad cheeks, counterweights, front snout/damper, rear flange, and flywheel
- pistons with crown, ring lands, pin band, thrust skirts, pin bosses, and wrist pin
- forged-rod silhouettes with big-end eye, separate cap/bolts, tapered I-beam web/flanges, and small-end eye
- a solid/cutaway aluminium head, aligned chamber regions, short port throats/bosses, angled valves, spring stacks, two camshafts, lobes, caps, and a cam cover

Intake/exhaust manifolds, turbocharging, accessories, cooling plumbing, combustion, valvetrain dynamics, oil flow, and dyno behavior remain outside this pass.

## Inspection modes

- **Full Engine** — opaque external castings and covers with external crank hardware.
- **Cutaway** — retained rear/section casting, end faces, deck lands, crankcase structure, and visible internals. It does not simulate a section by deleting the entire camera-facing wall.
- **Transparent Block / Head** — translucent full castings with the mechanism and valvetrain visible inside.
- **Rotating Assembly Only** — crankshaft, snout/damper, flywheel, pistons, pins, and rods.
- **Valvetrain Only** — isolated cams, caps, lobes, valves, spring stacks, chambers, and port throats with a tighter camera preset.

## Validity and fidelity

- Layout validity: inline-four prototype only.
- Geometry validity: intended for plausible conventional passenger-car bore/stroke/rod proportions, not extreme scale changes.
- Fidelity: teaching/game visualization. No claim of OEM dimensional accuracy, casting feasibility, collision clearance, structural adequacy, lubrication performance, or production tolerances.
- Materials: color/metalness classes distinguish cast iron, cast aluminium, machined steel, piston aluminium, bearing surfaces, seals, and dark internal cavities; they are not material-property inputs.

## Validation reference

At the baseline 86 mm bore × 86 mm stroke × 143 mm rod length:

- authoritative displacement remains approximately `1.9982288569 L`
- authoritative rod/stroke ratio remains `143 / 86`
- piston-pin and crank-pin positions remain direct results of Core slider-crank kinematics
- scene validation rebuilds the assembly at multiple geometry tuples and exercises all five inspection modes without changing controller operating RPM
