using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;

// SavWav sınıfını da burada tanımlayalım
public static class SavWav
{
    public static void Save(string filepath, AudioClip clip)
    {
        using (var fileStream = CreateEmpty(filepath))
        {
            ConvertAndWrite(fileStream, clip);
            WriteHeader(fileStream, clip);
        }
    }

    private static FileStream CreateEmpty(string filepath)
    {
        var fileStream = new FileStream(filepath, FileMode.Create);
        byte emptyByte = new byte();

        for (int i = 0; i < 44; i++)
        {
            fileStream.WriteByte(emptyByte);
        }

        return fileStream;
    }

    private static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        System.Int16[] intData = new System.Int16[samples.Length];
        System.Byte[] bytesData = new System.Byte[samples.Length * 2];

        const float rescaleFactor = 32767;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            System.Byte[] byteArr = System.BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        fileStream.Write(bytesData, 0, bytesData.Length);
    }

    private static void WriteHeader(FileStream fileStream, AudioClip clip)
    {
        var hz = clip.frequency;
        var channels = clip.channels;
        var samples = clip.samples;

        fileStream.Seek(0, SeekOrigin.Begin);

        System.Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        fileStream.Write(riff, 0, 4);

        System.Byte[] chunkSize = System.BitConverter.GetBytes(fileStream.Length - 8);
        fileStream.Write(chunkSize, 0, 4);

        System.Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        fileStream.Write(wave, 0, 4);

        System.Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        fileStream.Write(fmt, 0, 4);

        System.Byte[] subChunk1 = System.BitConverter.GetBytes(16);
        fileStream.Write(subChunk1, 0, 4);

        System.Byte[] audioFormat = System.BitConverter.GetBytes(1);
        fileStream.Write(audioFormat, 0, 2);

        System.Byte[] numChannels = System.BitConverter.GetBytes(channels);
        fileStream.Write(numChannels, 0, 2);

        System.Byte[] sampleRate = System.BitConverter.GetBytes(hz);
        fileStream.Write(sampleRate, 0, 4);

        System.Byte[] byteRate = System.BitConverter.GetBytes(hz * channels * 2);
        fileStream.Write(byteRate, 0, 4);

        System.Byte[] blockAlign = System.BitConverter.GetBytes(channels * 2);
        fileStream.Write(blockAlign, 0, 2);

        System.Byte[] bitsPerSample = System.BitConverter.GetBytes(16);
        fileStream.Write(bitsPerSample, 0, 2);

        System.Byte[] dataString = System.Text.Encoding.UTF8.GetBytes("data");
        fileStream.Write(dataString, 0, 4);

        System.Byte[] subChunk2 = System.BitConverter.GetBytes(samples * channels * 2);
        fileStream.Write(subChunk2, 0, 4);
    }
}