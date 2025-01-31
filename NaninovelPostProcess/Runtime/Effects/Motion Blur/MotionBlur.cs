﻿//2022 idaocracy

#if UNITY_POST_PROCESSING_STACK_V2

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Naninovel;
using Naninovel.Commands;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NaninovelPostProcess { 

    [RequireComponent(typeof(PostProcessVolume))]
    public class MotionBlur : MonoBehaviour, Spawn.IParameterized, Spawn.IAwaitable, DestroySpawned.IParameterized, DestroySpawned.IAwaitable
    {
        protected float Duration { get; private set; }
        protected float VolumeWeight { get; private set; }
        protected float ShutterAngle { get; private set; }
        protected float SampleCount { get; private set; }
        protected float FadeOutDuration { get; private set; }

        private readonly Tweener<FloatTween> volumeWeightTweener = new Tweener<FloatTween>();
        private readonly Tweener<FloatTween> shutterAngleTweener = new Tweener<FloatTween>();
        private readonly Tweener<FloatTween> sampleCountTweener = new Tweener<FloatTween>();

        [Header("Spawn/Fadein Settings")]
        [SerializeField] private float defaultDuration = 0.35f;
        [Header("Volume Settings")]
        [SerializeField] private float defaultVolumeWeight = 1f;
        [Header("Motion Blur Settings")]
        [SerializeField] private float defaultShutterAngle = 270f;
        [SerializeField] private float defaultSampleCount = 10f;
        [Header("Despawn/Fadeout Settings")]
        [SerializeField] private float defaultFadeOutDuration = 0.35f;

        private PostProcessVolume volume;
        private UnityEngine.Rendering.PostProcessing.MotionBlur motionBlur;

        public virtual void SetSpawnParameters(IReadOnlyList<string> parameters, bool asap)
        {
            Duration = asap ? 0 : Mathf.Abs(parameters?.ElementAtOrDefault(0)?.AsInvariantFloat() ?? defaultDuration);
            VolumeWeight = parameters?.ElementAtOrDefault(1)?.AsInvariantFloat() ?? defaultVolumeWeight;
            ShutterAngle = parameters?.ElementAtOrDefault(2)?.AsInvariantFloat() ?? defaultShutterAngle;
            SampleCount = parameters?.ElementAtOrDefault(2)?.AsInvariantFloat() ?? defaultSampleCount;
        }

        public async UniTask AwaitSpawnAsync(AsyncToken asyncToken = default)
        {
            CompleteTweens();
            var duration = asyncToken.Completed ? 0 : Duration;
            await ChangeDoFAsync(duration, VolumeWeight, ShutterAngle, SampleCount, asyncToken);
        }

        public async UniTask ChangeDoFAsync(float duration, float volumeWeight, float focusDistance, float focalLength, AsyncToken asyncToken = default)
        {
            var tasks = new List<UniTask>();
            if (volume.weight != volumeWeight) tasks.Add(ChangeVolumeWeightAsync(volumeWeight, duration, asyncToken));
            if (motionBlur.shutterAngle.value != focusDistance) tasks.Add(ChangeShutterAngleAsync(focusDistance, duration, asyncToken));
            if (motionBlur.sampleCount.value != focalLength) tasks.Add(ChangeSampleCountAsync(focalLength, duration, asyncToken));

            await UniTask.WhenAll(tasks);
        }

        public void SetDestroyParameters(IReadOnlyList<string> parameters)
        {
            FadeOutDuration = parameters?.ElementAtOrDefault(0)?.AsInvariantFloat() ?? defaultFadeOutDuration;
        }

        public async UniTask AwaitDestroyAsync(AsyncToken asyncToken = default)
        {
            CompleteTweens();
            var duration = asyncToken.Completed ? 0 : FadeOutDuration;
            await ChangeVolumeWeightAsync(0f, duration, asyncToken);
        }

        private void CompleteTweens()
        {
            if (shutterAngleTweener.Running) shutterAngleTweener.CompleteInstantly();
            if (sampleCountTweener.Running) sampleCountTweener.CompleteInstantly();
            if (volumeWeightTweener.Running) volumeWeightTweener.CompleteInstantly();
        }

        private void Awake()
        {
            volume = GetComponent<PostProcessVolume>();
            motionBlur = volume.profile.GetSetting<UnityEngine.Rendering.PostProcessing.MotionBlur>() ?? volume.profile.AddSettings<UnityEngine.Rendering.PostProcessing.MotionBlur>();
            motionBlur.SetAllOverridesTo(true);
            volume.weight = 0f;
        }
        private async UniTask ChangeVolumeWeightAsync(float volumeWeight, float duration, AsyncToken asyncToken = default)
        {
            if (duration > 0) await volumeWeightTweener.RunAsync(new FloatTween(volume.weight, volumeWeight, duration, x => volume.weight = x), asyncToken, volume);
            else volume.weight = volumeWeight;
        }
        private async UniTask ChangeShutterAngleAsync(float shutterAngle, float duration, AsyncToken asyncToken = default)
        {

            if (duration > 0) await shutterAngleTweener.RunAsync(new FloatTween(motionBlur.shutterAngle.value, shutterAngle, duration, x => motionBlur.shutterAngle.value = x), asyncToken, motionBlur);
            else motionBlur.shutterAngle.value = shutterAngle;
        }
        private async UniTask ChangeSampleCountAsync(float sampleCount, float duration, AsyncToken asyncToken = default)
        {

            if (duration > 0) await sampleCountTweener.RunAsync(new FloatTween((int)motionBlur.sampleCount.value, sampleCount, duration, x => motionBlur.sampleCount.value = (int)x), asyncToken, motionBlur);
            else motionBlur.sampleCount.value = (int)sampleCount;
        }

#if UNITY_EDITOR

        public string SceneAssistantParameters()
        {
            EditorGUIUtility.labelWidth = 190;
            GUILayout.BeginHorizontal();
            Duration = EditorGUILayout.FloatField("Fade-in time", Duration, GUILayout.Width(413));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Volume Weight", GUILayout.Width(190));
            volume.weight = EditorGUILayout.Slider(volume.weight, 0f, 1f, GUILayout.Width(220));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shutter Angle", GUILayout.Width(190));
            motionBlur.shutterAngle.value = EditorGUILayout.Slider(motionBlur.shutterAngle.value, 0f, 360f, GUILayout.Width(220));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sample Count", GUILayout.Width(190));
            motionBlur.sampleCount.value = (int)EditorGUILayout.Slider(motionBlur.sampleCount.value, 4, 32, GUILayout.Width(220));
            GUILayout.EndHorizontal();

            return Duration + "," + GetString();
        }

        public string GetString()
        {
            return volume.weight + "," + motionBlur.shutterAngle.value + "," + motionBlur.sampleCount.value;
        }
#endif
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MotionBlur))]
    public class CopyFXMotionBlur : Editor
    {
        private MotionBlur targetObject;
        private UnityEngine.Rendering.PostProcessing.MotionBlur motionBlur;
        private PostProcessVolume volume;
        public bool LogResult;

        private void Awake()
        {
            targetObject = (MotionBlur)target;
            volume = targetObject.gameObject.GetComponent<PostProcessVolume>();
            motionBlur = volume.profile.GetSetting<UnityEngine.Rendering.PostProcessing.MotionBlur>();

        }

        public override void OnInspectorGUI()
        {

            GUILayout.Space(20f);
            if (GUILayout.Button("Copy command and params (@)", GUILayout.Height(50)))
            {
                if (motionBlur != null) GUIUtility.systemCopyBuffer = "@spawn " + targetObject.gameObject.name + " params:(time)," + targetObject.GetString();
                if (LogResult) Debug.Log(GUIUtility.systemCopyBuffer);
            }

            GUILayout.Space(20f);
            if (GUILayout.Button("Copy command and params ([])", GUILayout.Height(50)))
            {
                if (motionBlur != null) GUIUtility.systemCopyBuffer = "[spawn " + targetObject.gameObject.name + " params:(time)," + targetObject.GetString() + "]"; 
                if (LogResult) Debug.Log(GUIUtility.systemCopyBuffer);
            }

            GUILayout.Space(20f);
            if (GUILayout.Button("Copy params", GUILayout.Height(50)))
            {
                if (motionBlur != null) GUIUtility.systemCopyBuffer = "(time)," + targetObject.GetString();
                if (LogResult) Debug.Log(GUIUtility.systemCopyBuffer);
            }

            GUILayout.Space(20f);
            if (GUILayout.Toggle(LogResult, "Log Results")) LogResult = true;
            else LogResult = false;

            base.DrawDefaultInspector();
        }
    }

#endif

}

#endif