using System;
using System.IO;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public static class WavWriter
{
    /// <summary>
    /// Saves a mono or interleaved buffer to a .wav file. Written by GPT-5, with some small modifications.
    /// </summary>
    /// <param name="fullPath">Full file path including .wav extension.</param>
    /// <param name="samples">Audio samples. If channels > 1, provide interleaved samples.</param>
    /// <param name="sampleRate">Samples per second (e.g., 44100 or 48000).</param>
    /// <param name="channels">Number of channels (1 = mono, 2 = stereo, etc.).</param>
    /// <param name="writeFloat32">True = 32-bit IEEE float WAV (format code 3). False = 16-bit PCM (format code 1).</param>
    public static void Save(string fullPath, float[] samples, int sampleRate, int channels = 1, bool writeFloat32 = true, bool writeMono = false, bool echogram = false)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples array is null or empty.");
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

        int outChannels = writeMono ? 1 : channels;
        int outStride = writeMono ? channels : 1;

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var bw = new BinaryWriter(fs))
        {
            int bitsPerSample = writeFloat32 ? 32 : 16;
            ushort audioFormat = writeFloat32 ? (ushort)3 : (ushort)1; // 3 = IEEE float, 1 = PCM
            int byteRate = sampleRate * outChannels * (bitsPerSample / 8);
            short blockAlign = (short)(outChannels * (bitsPerSample / 8));
            int dataByteCount = samples.Length * (bitsPerSample / 8);
            int riffChunkSize = 36 + dataByteCount;

            // --- RIFF header ---
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffChunkSize); // little-endian
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // --- fmt  chunk ---
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                          // Subchunk1Size for PCM/float
            bw.Write(audioFormat);                 // AudioFormat
            bw.Write((ushort)outChannels);         // NumChannels
            bw.Write(sampleRate);                  // SampleRate
            bw.Write(byteRate);                    // ByteRate
            bw.Write(blockAlign);                  // BlockAlign
            bw.Write((ushort)bitsPerSample);       // BitsPerSample

            // --- data chunk ---
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataByteCount);

            if (writeFloat32)
            {
                // Directly write float samples (assumed in [-1, 1], but not required)
                for (int i = 0; i < samples.Length; i += outStride)
                    bw.Write(Mathf.Pow(samples[i], echogram ? 2f : 1f));
            }
            else
            {
                // Convert [-1,1] floats to 16-bit PCM with clipping
                for (int i = 0; i < samples.Length; i += outStride)
                {
                    float s = Mathf.Clamp(Mathf.Pow(samples[i], echogram ? 2f : 1f), -1f, 1f);
                    short val = (short)Mathf.RoundToInt(s * 32767f);
                    bw.Write(val);
                }
            }
        }
    }
}
