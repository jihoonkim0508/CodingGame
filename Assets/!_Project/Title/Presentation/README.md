# Title presentation

The `Title` scene uses the project's existing title logo and `GameFlow` Start/Quit bindings.
The supplied background and button-design references were used to create five new PNGs
with the built-in image_gen tool. Original images and the complete prompts are preserved at
`C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/`.

## Layers and limits

- `BattlefieldClean`: fixed empty battlefield and distant city.
- `FriendlyLayer` / `EnemyLayer`: preserved first-version whole-faction extractions.
- `Depth/FriendlyRear`, `FriendlyMid`, `FriendlyFront`: base, middle robots, blurred foreground.
- `Depth/EnemyRear`, `EnemyMid`, `EnemyFront`: boss/banner, middle army, blurred foreground.
- `StartButton`, `QuitButton`: blank UI frames, with editable Korean TMP labels and UI icons.
- `Ambient/SoftSmoke`: a separate transparent atmospheric smoke sprite, generated from the battlefield style reference.

These are AI-edited illustration layers, not pixel-exact masks or animated 3D characters.
Six depth groups now enter at different times with a fast approach and small landing recoil.
Delays are 0, .08, .24, .36, .58 and .72 seconds after the faction sequence starts;
durations range from .54 to 1.08 seconds. The base remains anchored during idle.
Idle motion is a subpixel vertical drift of the robot groups and
slow sparse edge embers. Six low-opacity smoke sprites drift and cross-fade in pairs over
the existing illustration. The original painted smoke/fire and individual robot limbs remain static.
Two localized additive overlays pulse only cyan base lights and red enemy eyes/weapons.
The base's existing cyan code rows are highlighted in sequence. The code text is not replaced.
Flag cloth uses localized UV deformation with its pole-side pinned; the rest of the illustration
is stationary. The middle friendly turret's upper body is separated by an exact source-UV region
and drawn as a pivoted RawImage for aiming and recoil, with a small brief muzzle flash every 6.4s.
No source PNG pixels are rewritten for those parts. Eight smoke sprites now form four pairs,
including two pairs in front of the edge units, with opacity .23-.30 and 5.5-7s drift cycles.
The corresponding runtime materials are cloned, so shared material assets are not animated in place.
The opening holds pure black for .4 seconds before DF fades in. DF's fully visible hold
is 1.15 seconds; total intro duration is 5.9875 seconds by default.
The PNG source pixels remain intact; button transparency padding is excluded by sprite rects.

## Editing

`Title Presentation` contains the live layout. `Title_OriginalLayout` retains the original
Canvas in an inactive state. `DF Team Splash/DF Team Logo` uses the supplied transparent
`Textures/Team/DFTeamLogo.png` unchanged. The sprite rect trims transparent padding and
the Image preserves the original aspect ratio. The scene saves the initial black state.

The controller Inspector exposes all intro durations and idle-motion strength. Intro timing
uses unscaled time. Click, Space, Enter, Escape, or gamepad Start/South skips ONLY the team
logo splash. The battlefield fade, staggered army entrance, title and menu then play normally.
The skip hint is hidden after the splash; repeated input cannot bypass those later beats.
Menu interaction
is blocked during the intro and for the input-consumption frames after completion.
Start/Quit buttons are mouse-only: Navigation.None, no automatic selected object,
and the Title EventSystem's move/submit/cancel actions are unbound. Pointer/left-click
remain bound and hover/press scaling responds only to pointer events. Splash-only skip
keys remain separate from menu activation.
`GameStartBtn` and `GameQuitBtn` retain their required names for `GameFlow`.
The background uses aspect-fill; UI uses a 1920x1080 scaling reference independently.

`Tools > Title > Rebuild Layered Title Scene` reconstructs only this presentation and keeps
the original layout group. Rebuilding this scene does not create a Player build.
`Tools > Title > Update Team Logo Only` updates the splash in place and preserves the rest
of the existing layout. A live scene copy is saved before the splash update.
`Tools > Title > Update Mouse Menu and Projectiles` updates only those controls/effects.
The turret now fires three projectiles .16s apart per 6.4s cycle, with matching recoil/flash.
TitleProjectileEffect draws the short cyan trails and small impact sparks inside the
battlefield layer, behind the title/buttons, with raycastTarget disabled.
Mouse hover keeps the button scale response and shows one filled cyan triangle beside
the hovered button, using play-icon proportions in a single color without a border.
The Start button contains only centered text. The outside button outline is disabled (`showOutline = false`). The extracted
sprite contour remains available for later use. The marker graphic ignores raycasts.
`TitlePlayVerification` runs only in a development Player with the explicit
`--title-verify <reports-directory>` command-line argument.
`TitleMotionRenderProbe.Begin` is an editor-only opt-in render check. It adds a temporary
component during Play, captures after WaitForEndOfFrame, and is never saved in the scene.

## Attribution

Background and menu-design reference images: user-provided.
Title logo and existing project assets: reused from the project.
New layered illustration/button PNGs: built-in image_gen edits of user-provided references.
DF team logo PNG: user-provided, copied without pixel edits.
