//-----------------------------------------------------------------------
// <copyright file="TiltToSpin.cs" company="Avantis Systems Ltd">
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
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Tilt-to-spin: tilting the head towards a shoulder turns the viewer about the vertical.
    /// </summary>
    /// <remarks>
    /// The plug-in does the turning and adds it to the head pose it reports, so the camera
    /// turns with no rig or script of the app's. It is on from the start unless the project's
    /// Cardboard settings (Tilt To Spin) say otherwise, and this class switches it while the
    /// app runs. An app that turns a rig from the head's tilt itself must switch this off, or
    /// stop doing so, or the viewer turns twice.
    ///
    /// It is done in the plug-in because of reprojection. A turn the app applies to a rig moves
    /// on only with each of the app's frames, so the world steps round whenever the app renders
    /// more slowly than the display refreshes. This turn moves on at every refresh and is
    /// reprojected as a turn of the head is. Without reprojection it behaves as a rig would.
    ///
    /// <see cref="Api.Recenter"/> undoes the turn along with recentring the head tracker.
    /// </remarks>
    public static class TiltToSpin
    {
        /// <summary>
        /// How far the head tilts before the turn starts, in degrees, unless told otherwise.
        /// </summary>
        public const float DefaultThresholdDegrees = 14.3f;

        /// <summary>
        /// Top speed of the turn in degrees a second, unless told otherwise. The speed rises
        /// with the tilt past the threshold and falls as the head looks up or down, so it is
        /// not reached in practice.
        /// </summary>
        public const float DefaultDegreesPerSecond = 229f;

        /// <summary>
        /// Gets a value indicating whether the plug-in can do the turning: on an Android
        /// device only. Where it cannot, the app must turn a rig itself.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets how far the viewer has been turned to their right, in degrees from -180 to
        /// 180. The head pose already includes it.
        /// </summary>
        public static float YawDegrees
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return CardboardUnity_getTiltToSpinYaw() * Mathf.Rad2Deg;
#else
                return 0f;
#endif
            }
        }

        /// <summary>
        /// Switches the turning on, or changes its threshold and speed. It stays on, across
        /// scenes, until <see cref="Disable"/>.
        /// </summary>
        /// <param name="thresholdDegrees">
        /// How far the head tilts before the turn starts.
        /// </param>
        /// <param name="degreesPerSecond">Top speed of the turn.</param>
        public static void Enable(
            float thresholdDegrees = DefaultThresholdDegrees,
            float degreesPerSecond = DefaultDegreesPerSecond)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CardboardUnity_setTiltToSpin(
                true, thresholdDegrees * Mathf.Deg2Rad, degreesPerSecond * Mathf.Deg2Rad);
#endif
        }

        /// <summary>
        /// Stops the turning. The viewer stays turned as they are: see <see cref="Reset"/>.
        /// </summary>
        public static void Disable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CardboardUnity_setTiltToSpin(false, 0f, 0f);
#endif
        }

        /// <summary>
        /// Undoes the turn, so the viewer faces the way the head tracker says.
        /// </summary>
        public static void Reset()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CardboardUnity_resetTiltToSpin();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(ApiConstants.CardboardApi)]
        private static extern void CardboardUnity_setTiltToSpin(
            [MarshalAs(UnmanagedType.I1)] bool enabled, float thresholdRadians,
            float radiansPerSecond);

        [DllImport(ApiConstants.CardboardApi)]
        private static extern void CardboardUnity_resetTiltToSpin();

        [DllImport(ApiConstants.CardboardApi)]
        private static extern float CardboardUnity_getTiltToSpinYaw();
#endif
    }
}
