using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ChapolinIntro
{
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavFile, string clipName = "wav")
        {
            // Parse header
            int channels = BitConverter.ToInt16(wavFile, 22);
            int sampleRate = BitConverter.ToInt32(wavFile, 24);
            int bitDepth = BitConverter.ToInt16(wavFile, 34);
            
            Debug.Log($"Channels: {channels}, SampleRate: {sampleRate}, BitDepth: {bitDepth}");

            if (bitDepth != 16)
            {
                Debug.LogError("[WavUtility] Only 16-bit WAV supported");
                return null;
            }

            // Find data chunk ("data" marker)
            int pos = 12; 
            while (!(wavFile[pos] == 'd' && wavFile[pos + 1] == 'a' &&
                     wavFile[pos + 2] == 't' && wavFile[pos + 3] == 'a'))
            {
                pos += 4;
                int chunkSize = BitConverter.ToInt32(wavFile, pos);
                pos += 4 + chunkSize;
            }
            pos += 8; // skip "data" and size

            // PCM samples → float[]
            int sampleCount = (wavFile.Length - pos) / 2; 
            float[] data = new float[sampleCount];
            int offset = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavFile, pos + offset);
                data[i] = sample / 32768f;
                offset += 2;
            }

            // Create AudioClip
            var clip = AudioClip.Create(clipName, sampleCount / channels, channels, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
    
    
    [BepInPlugin("ChapolinIntro", "Chapolin Intro", "1.0.0")]
    [BepInProcess("valheim.exe")]
    public class ValheimMod : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new Harmony("ChapolinIntro");
        private static AudioClip introClip;
        private static string introPath = "ChapolinIntro.Assets.intro.wav";
        private static AudioSource source;
        
        public static AudioClip LoadWavFromResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Debug.LogError($"[EmbeddedWavLoader] Resource not found: {resourceName}");
                    return null;
                }

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return WavUtility.ToAudioClip(ms.ToArray(), resourceName);
                }
            }
        }
        
        private void Awake()
        {
            introClip = LoadWavFromResource(introPath);
            source = gameObject.AddComponent<AudioSource>();
            source.volume = 0.5f;
            
            Logger.LogInfo($"Plugin Chapolin Intro loaded!");
            _harmony.PatchAll();
        }

        private static void CheckAndChangeText(ref string text)
        {
            if (text.Equals("I have arrived!") || text.Equals("Eu Cheguei!"))
            {
                text = "Nao contavam com minha ASTUCIA!";
                source.PlayOneShot(introClip);
                Debug.Log("Yep correct, play song...");
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
        class Chat_Patch
        {
            static void Prefix(ref string text)
            {
                CheckAndChangeText(ref text);
            }
        }
        
        [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
        class ChatSend_Patch
        {
            static void Prefix(ref string text)
            {
                CheckAndChangeText(ref text);
            }
        }
    }    
}