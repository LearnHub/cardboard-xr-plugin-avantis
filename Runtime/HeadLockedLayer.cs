//-----------------------------------------------------------------------
// <copyright file="HeadLockedLayer.cs" company="Avantis Systems Ltd">
// Copyright 2026 Avantis Systems Ltd
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//-----------------------------------------------------------------------

namespace Google.XR.Cardboard
{
    using System;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// A flat picture fixed to the head, for a reticle.
    /// </summary>
    /// <remarks>
    /// With reprojection on, what the cameras draw is turned to follow the head between the
    /// app's frames, so anything drawn in the scene to stay in the middle of the view shakes
    /// whenever the app renders more slowly than the display refreshes. The plug-in draws this
    /// layer itself, after that turn and on every refresh, so it stays put.
    ///
    /// The layer is a rectangle straight ahead of the point midway between the eyes, facing it.
    /// Each eye sees it from where that eye is, so it appears at its distance, as an object in
    /// the scene there would. It is drawn over everything: nothing in the scene can hide it.
    /// It is not part of the ClassView picture.
    ///
    /// Call <see cref="Show"/> every frame the layer should be seen, and draw the same thing
    /// in the scene instead whenever <see cref="IsAvailable"/> is false.
    /// </remarks>
    public static class HeadLockedLayer
    {
        /// <summary>
        /// Gets a value indicating whether the layer will be shown: only while reprojection is
        /// drawing the display (Android with OpenGL ES 3, and reprojection switched on). It can
        /// change while the app runs, so check it every frame.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return CardboardUnity_isReprojectionPresenting();
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Shows the layer from the next frame on. Call it every frame: the layer goes by
        /// itself a few frames after the last call.
        /// </summary>
        /// <remarks>
        /// The texture is read while the app carries on, so a picture that changes should be
        /// drawn into a different texture each time (two or three taken in turn), not over the
        /// one being shown. A change of distance, size or tint needs no new texture. A texture
        /// much larger than the layer appears on the display should have mipmaps, or it will
        /// shimmer. Call <see cref="Hide"/> before destroying the texture.
        /// </remarks>
        /// <param name="texture">The picture: a Texture2D or a RenderTexture.</param>
        /// <param name="distance">
        /// How far ahead of the head the layer is, in metres of the head's own space: divide a
        /// distance in the scene by the scale of the camera rig.
        /// </param>
        /// <param name="size">Width and height of the layer at that distance, in metres.</param>
        /// <param name="tint">
        /// Multiplies the picture's colour and opacity, as it is seen (not in linear light).
        /// </param>
        /// <param name="premultipliedAlpha">
        /// Whether the texture's colours are already multiplied by its opacity, as they are in
        /// a RenderTexture a camera has drawn over transparent black. False for an ordinary
        /// imported texture.
        /// </param>
        public static void Show(
            Texture texture, float distance, Vector2 size, Color tint,
            bool premultipliedAlpha = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (texture == null || distance <= 0f || size.x <= 0f || size.y <= 0f)
            {
                Hide();
                return;
            }

            // In a linear project what the texture gives is linear light, whichever format it
            // is kept in; in a gamma project it is already as the display wants it.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            CardboardUnity_setHeadLockedLayer(
                texture.GetNativeTexturePtr(), distance, size.x, size.y, tint.r, tint.g, tint.b,
                tint.a, premultipliedAlpha, linear);
#endif
        }

        /// <summary>
        /// Takes the layer away from the next frame on.
        /// </summary>
        public static void Hide()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CardboardUnity_clearHeadLockedLayer();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(ApiConstants.CardboardApi)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CardboardUnity_isReprojectionPresenting();

        [DllImport(ApiConstants.CardboardApi)]
        private static extern void CardboardUnity_setHeadLockedLayer(
            IntPtr texture, float distance, float width, float height, float red, float green,
            float blue, float alpha, [MarshalAs(UnmanagedType.I1)] bool premultipliedAlpha,
            [MarshalAs(UnmanagedType.I1)] bool linear);

        [DllImport(ApiConstants.CardboardApi)]
        private static extern void CardboardUnity_clearHeadLockedLayer();
#endif
    }
}
