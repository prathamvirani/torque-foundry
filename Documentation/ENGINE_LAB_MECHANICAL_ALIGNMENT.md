# Engine Lab mechanical alignment and kinematic correctness

## Scope

This pass corrects the assembly relationships in the chain-driven compact DOHC inline-four introduced by Visual Fidelity Pass v2. It adds no new engine feature and does not change the authoritative bore, stroke, connecting-rod length, cylinder spacing, displacement, or slider-crank equations.

## Coordinate convention

All generated presentation geometry uses one Engine Lab root-local, right-handed coordinate system in metres:

- **X:** crankshaft axis and cylinder-number direction; the timing end is negative X and the flywheel end is positive X.
- **Y:** cylinder-bore axis and positive piston travel; the deck and head are above the crankshaft.
- **Z:** engine depth; negative Z is exhaust and positive Z is intake.

The `Engine Lab` scene root remains position/rotation zero with unit scale. World transforms are never authoritative engineering state.

## Shared datums

`InlineFourVisualFidelityAssembly` builds the following local datums before it creates any procedural mesh:

- crankshaft centerline
- four bore centerlines
- five main-bearing centers
- deck plane
- four combustion-chamber centers
- sixteen valve-seat points and valve axes
- intake and exhaust camshaft axes
- timing-drive plane
- front/timing face
- rear/flywheel face

The optional `Engineering Datums` generated group displays these axes and plane outlines. It is disabled by default and is presentation/debug state only.

## Mechanical chains

### Bore to crankshaft

Each piston root and wrist pin is concentric with its bore datum. A connecting rod is rooted at the authoritative crank-pin position and rotated so its local center-to-center length terminates exactly at the wrist pin. Cylinders 1/4 retain the same crank phase, cylinders 2/3 retain the opposite phase, and firing order remains 1-3-4-2 over 720 crank degrees.

Piston pin bosses are now coaxial with the wrist pin. Main journals, bearing saddles, and bulkheads use the same five bearing datums.

### Crankcase clearance

The retained cutaway wall is not allowed to define or clip the mechanism. Minimum crankcase depth is derived from:

`crank radius + connecting-rod big-end outer radius + presentation running clearance + casting wall`

The calculation also accounts for the narrowest retained-wall profile around the crank sweep. This is a presentation packaging assumption, not an engineering input, and it never feeds back into Core.

### Valve train

Each of the sixteen valve mechanisms uses one chain:

`cam lobe → direct bucket follower → valve stem/retainer → valve head/seat`

The cam axis is derived from the valve seat, valve axis, stem length, follower thickness, and cam base radius. The lobe clocking places the lobe nose on the bucket at the deterministic peak-lift crank angle. The rendered valve head terminates at the seat datum when closed and moves into the chamber along the stem axis. At the 86 mm bore baseline, presentation maximum lift remains 9.03 mm. Springs scale from their stored closed length and visibly shorten with lift.

This is deterministic geometric kinematics only. It does not simulate contact force, lash, follower inertia, spring surge, deformation, wear, VTC, or VTEC.

### Ports and timing drive

Every port path ends at its associated valve seat through a named seat-throat segment. Port and airflow teaching meshes share the same opening/runner/bowl/seat path.

The crank sprocket is concentric with the crankshaft datum. Both cam sprockets are concentric with their cam axes, and all three sprockets share the timing plane. The 24/48-tooth definition preserves the exact 2:1 crank-to-cam speed relationship. Chain guides and the tensioner remain reduced visual representations; individual chain-link dynamics are not modeled.

## Validation and visual regression

The scene validator now checks actual generated transforms in addition to pure-C# kinematics:

- bore/piston/wrist-pin/rod/crank-pin concentricity for all cylinders
- configured rod center distance
- main journal and bearing alignment
- I4 1/4 and 2/3 piston pairing
- 1-3-4-2 firing TDC sequence at 0/180/360/540 degrees
- rendered valve-head seating, opening, peak lift, closing, and exact return for all sixteen valves
- spring compression and lobe/bucket contact at peak lift
- sixteen complete cam/follower/valve associations
- intake and exhaust port termination at the correct seat
- sprocket concentricity, coplanarity, and 2:1 relationship
- crankcase retained-wall clearance across the complete crank rotation
- hierarchy replacement and no stale procedural meshes after bore/stroke/rod rebuilds

Fixed-camera captures are generated at 0, 90, 180, 360, 540, and 720/0 degrees for Cutaway, Rotating Assembly Only, Valvetrain Only, and a front timing-drive close-up. A separate normal-editor Play Mode audit observes one continuous 720-degree cycle and requires every rendered intake and exhaust valve to reach visible peak lift and a fully seated state.

## Remaining visual limits

- Clearances are presentation packaging checks, not collision/contact simulation.
- The cutaway deliberately retains a far-side casting wall; some internal parts can be occluded by that wall from certain camera angles, but the validated moving envelope remains inside it.
- Procedural shapes remain reduced visual abstractions rather than manufacturable CAD.
- Valve-to-piston clearance, bearing oil clearance, thermal growth, elastic deformation, and tolerance stacks are not modeled.
