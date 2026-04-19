using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;

namespace Skeleton_Jumpscare;

// TODO - adjust the plugin guid as needed
[BepInAutoPlugin(id: "io.github.drb1220.skeleton_jumpscare")]
public partial class Skeleton_JumpscarePlugin : BaseUnityPlugin
{
    private readonly List<Texture2D> frames = new();
    private int currentFrame = 0;
    private bool isPlaying = false;
    private float frameTimer = 0f;

    private const float FramesPerSecond = 60f;
    private AudioSource? audioSource;

    private readonly System.Random rng = new();
    private ConfigEntry<float> averageSecondsBetweenTriggers = null!;

    private void Awake()
    {
        string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string framesFolder = Path.Combine(pluginFolder, "Frames");
        string audioPath = Path.Combine(pluginFolder, "bad.wav");

        LoadFrames(framesFolder);

        GameObject audioObject = new GameObject("SkeletonJumpscareAudio");
        DontDestroyOnLoad(audioObject);
        audioSource = audioObject.AddComponent<AudioSource>();

        StartCoroutine(LoadAudio(audioPath));

        Logger.LogInfo($"Loaded {frames.Count} jumpscare frames.");

        averageSecondsBetweenTriggers = Config.Bind(
            "General",
            "AverageSecondsBetweenTriggers",
            250f,
            "Average number of seconds between random running skeleton events."
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            PlayJumpscare();
        }

        if (isPlaying)
        {
            frameTimer += Time.deltaTime;

            float frameDuration = 1f / FramesPerSecond;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame++;

                if (currentFrame >= frames.Count)
                {
                    StopJumpscare();
                    break;
                }
            }

            return;
        }

        float avg = Mathf.Max(0.01f, averageSecondsBetweenTriggers.Value);
        float chanceThisFrame = Time.deltaTime / avg;

        if (rng.NextDouble() < chanceThisFrame)
        {
            PlayJumpscare();
        }
    }

    private void OnGUI()
    {
        if (!isPlaying || frames.Count == 0 || currentFrame >= frames.Count)
            return;

        Texture2D frame = frames[currentFrame];

        GUI.DrawTexture(
            new Rect(0, 0, Screen.width, Screen.height),
            frame,
            ScaleMode.ScaleAndCrop, // best choice here
            true
        );
    }

    private void PlayJumpscare()
    {
        if (frames.Count == 0)
        {
            Logger.LogWarning("No frames loaded.");
            return;
        }

        currentFrame = 0;
        frameTimer = 0f;
        isPlaying = true;

        if (audioSource?.clip != null)
        {
            audioSource.Stop();
            audioSource.Play();
        }

        Logger.LogInfo("Playing jumpscare.");
    }

    private void StopJumpscare()
    {
        isPlaying = false;
        currentFrame = 0;
        frameTimer = 0f;

        Logger.LogInfo("Jumpscare finished.");
    }

    private void LoadFrames(string framesFolder)
    {
        if (!Directory.Exists(framesFolder))
        {
            Logger.LogError($"Frames folder not found: {framesFolder}");
            return;
        }

        string[] files = Directory.GetFiles(framesFolder, "*.png");
        System.Array.Sort(files);

        foreach (string file in files)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(bytes);

                frames.Add(texture);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to load frame {file}: {ex}");
            }
        }
    }

    private IEnumerator LoadAudio(string path)
    {
        if (!File.Exists(path))
        {
            Logger.LogError($"Audio file not found: {path}");
            yield break;
        }

        using UnityWebRequest www =
            UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            if (audioSource != null)
            {
                audioSource.clip = DownloadHandlerAudioClip.GetContent(www);
                Logger.LogInfo("Loaded jumpscare audio.");
            }
        }
        else
        {
            Logger.LogError("Audio load failed: " + www.error);
        }
    }
}
