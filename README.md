Avantis Custom Google Cardboard XR Plugin for Unity
===================================================
This branch, `develop`, holds the code for Avantis' modified Cardboard XR Plugin.

For the upstream/original cardboard code, see the "master" branch.

## Native Cardboard SDK

This project relies on this native Cardboard SDK [as an AAR file](Runtime/Android/GfxPluginCardboard.aar).
This AAR is created from the [`develop` branch of this fork](https://github.com/LearnHub/cardboard-sdk-avantis).

### Updating the AAR

When the native SDK is changed, the AAR must be rebuilt and copied here. With both repositories
checked out side by side:

```bash
cd cardboard-sdk-avantis
./gradlew :sdk:clean :sdk:copyAarToUnityPlugin
```

This builds the release AAR and copies it to `Runtime/Android/GfxPluginCardboard.aar`. If this
repository is checked out elsewhere, add `-PunityPluginDir=<path to this repository>`.

Then commit the updated AAR to this repo and bump the version in `package.json`.

## Reprojection

Without reprojection the display shows a new picture only when Unity finishes a frame, so an app
that renders more slowly than the display refreshes judders whenever the head turns. With it, the
plug-in redraws the last finished frame on every refresh, turned to where the head is by then, so
head movement stays smooth at the display's rate (72 Hz on a ClassVR headset) whatever rate the
app manages.

It is off by default and is chosen once, when the app starts:

- **In the Unity project:** tick *Reprojection* in the Cardboard XR Plugin settings (Project
  Settings, XR Plug-in Management, Cardboard XR Plugin). The setting is baked into the build.
- **On a device:** the system setting `CLASSVR_CARDBOARD_REPROJECTION` overrides the build: `1`
  turns it on and `0` turns it off. Remove the setting to go back to what the build asks for.
  The app must be restarted for a change to take effect.

  ```bash
  adb shell settings put system CLASSVR_CARDBOARD_REPROJECTION 1
  adb shell settings delete system CLASSVR_CARDBOARD_REPROJECTION
  ```

What to expect:

- **OpenGL ES 3 on Android only.** Under Vulkan the setting does nothing. Single-pass and
  multi-pass rendering both work.
- **Only head rotation is corrected.** Things that move in the scene, and the camera rig when the
  app turns or moves it, still update at the app's own frame rate. Anything drawn in the scene
  to stay fixed to the head, such as a reticle or a heads-up display, will shake when the app
  drops frames. For a reticle, use the head-locked layer described below.
- **Soft edges.** When the head turns quickly and the app is slow, the part of the view that has
  not been rendered yet is filled by carrying the edge of the picture outwards and fading it to
  black, until the next frame arrives.
- **Cost.** About 105 MB more graphics memory (a second full-screen surface, and three sets of
  eye textures instead of one), and a GPU-bound app renders roughly 10 to 15% fewer frames,
  because the display is now redrawn 72 times a second however slow the app is. Measured on a
  CVR355128 at the Balanced (58%) resolution.

The picture is drawn on a second surface laid over Unity's own while VR is running, by a thread in
the native SDK; see `sdk/unity/android/reprojection.h` in the native SDK for how it works. If that
surface cannot be set up, the plug-in presents through Unity as it does with reprojection off.

### Head-locked layer (reticle)

`Google.XR.Cardboard.HeadLockedLayer` draws one flat picture fixed to the head: a rectangle
straight ahead of the point midway between the eyes. The plug-in draws it after the picture has
been turned for the head, on every refresh, so it stays in the middle of the view however slowly
the app renders. Each eye sees it from where that eye is, so it appears at the distance given.

```csharp
if (HeadLockedLayer.IsAvailable)
{
    // Every frame the reticle should show: texture, metres ahead, size in metres, tint
    HeadLockedLayer.Show(reticleTexture, hitDistance, new Vector2(0.06f, 0.06f), Color.white);
}
else
{
    // Reprojection is not drawing the display: draw the reticle in the scene as before
}
```

- **Check `IsAvailable` every frame.** It is true only while reprojection is drawing the display,
  and can change while the app runs. When it is false the layer is not shown at all.
- **Call `Show` every frame.** The layer goes by itself a few frames after the last call, and
  `Hide` takes it away at once. A change of distance, size or tint needs no new texture.
- **A picture that changes** should be drawn into a different texture each time (two or three
  taken in turn), not over the one being shown. Pass `premultipliedAlpha: true` for a
  RenderTexture that a camera has drawn over transparent black.
- **It is drawn over everything.** Nothing in the scene can hide it, and it is not part of the
  ClassView picture.
- **The distance** is in metres of the head's own space: divide a distance in the scene by the
  scale of the camera rig.

To check the geometry on a headset lying still, set `debug.avn.reprojection.test` before starting
the app: `1` replaces the head tracker with a head that sways and reprojects it away, so the
picture should stand still; `2` shows the same sway without the correction. Set it back to `0`
afterwards.

Original Readme:
================

> [!IMPORTANT]
> Do not file issues here. Please file issues at the [Cardboard SDK repository](https://github.com/googlevr/cardboard/issues) to report any bugs, ask questions or request specific features.

Google Cardboard XR Plugin for Unity
====================================
Copyright 2020 Google LLC

This SDK provides everything you need to create your own Virtual Reality (VR)
experiences for Google Cardboard in Unity 6000.0.58f2 or later. It supports
essential VR features, such as:

 * Motion tracking
 * Stereoscopic rendering
 * User interaction via the viewer button

With these capabilities you can build entirely new VR experiences, or enhance
existing apps with VR capabilities.


## Get started

To get started with the Google Cardboard XR Plugin for Unity, see:

* [Quickstart for Unity](//developers.google.com/cardboard/develop/unity/quickstart)


## API reference

* [Google Cardboard XR Plugin for Unity API Reference](//developers.google.com/cardboard/reference/unity)


## Release notes

The SDK release notes are available on the
[releases](//github.com/googlevr/cardboard-xr-plugin/releases) page.


## Roadmap

The project roadmap is available on the
[Projects](https://github.com/googlevr/cardboard/projects/1) page.


## Contributing

Please read and follow the steps in [CONTRIBUTING.md](/CONTRIBUTING.md) file.


## License

Please see the [LICENSE.md](/LICENSE.md) file.


## Brand guidelines

The "Google Cardboard" name is a trademark owned by Google and is not included
within the assets licensed under the Apache License 2.0. Cardboard is a free
and open-source SDK that developers can use to create apps that are compatible
with the Google Cardboard VR platform. At the same time, it's important to make
sure that people don't use the "Google Cardboard" mark in ways that could
create confusion.

The guidelines below are designed to clarify the permitted uses of the "Google
Cardboard" mark.

**Things you can do**:

* Use the "Google Cardboard" mark to describe or refer to apps developed with
  the Cardboard SDK in ways that would be considered "fair use."
* Use the "Google Cardboard" mark to make truthful factual statements regarding
  the compatibility or interoperability of your app. For example, "This app is
  compatible with Google Cardboard" or "This app works with Google Cardboard."

**Things you can't do**:

* Don't use the "Google Cardboard" mark in a manner that implies that Google has
  endorsed or authorized your app or that makes your app appear to be an
  official Google product. For example, you shouldn't reference your product as
  "an official Google Cardboard app."
* Don't incorporate the "Google Cardboard" mark into your own product names,
  service names, trademarks, logos, or company names.
* Don't display the "Google Cardboard" mark in a manner that is misleading,
  unfair, defamatory, infringing, libelous, disparaging, obscene or otherwise
  objectionable to Google.
* Don't alter or distort the "Google Cardboard" mark. This includes modifying
  the mark through hyphenation, combination or abbreviation. For example, don't
  say "G Cardboard" or "Google-Cardboard."

In addition to these guidelines, please ensure that you follow the trademark
usage guidelines available here:
https://www.google.com/permissions/logos-trademarks/.
