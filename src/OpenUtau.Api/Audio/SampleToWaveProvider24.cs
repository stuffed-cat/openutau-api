using System;
using NAudio.Wave;

namespace OpenUtau.Api
{
    public class SampleToWaveProvider24 : IWaveProvider
    {
        private ISampleProvider source;
        private WaveFormat waveFormat;

        public SampleToWaveProvider24(ISampleProvider source)
        {
            this.source = source;
            this.waveFormat = new WaveFormat(source.WaveFormat.SampleRate, 24, source.WaveFormat.Channels);
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int sourceSamples = count / 3;
            float[] sampleBuffer = new float[sourceSamples];
            int samplesRead = source.Read(sampleBuffer, 0, sourceSamples);

            int outIndex = offset;
            for (int i = 0; i < samplesRead; i++)
            {
                float sample = sampleBuffer[i];
                if (sample > 1.0f) sample = 1.0f;
                if (sample < -1.0f) sample = -1.0f;
                int intSample = (int)(sample * 8388607.0f);
                buffer[outIndex++] = (byte)(intSample & 0xFF);
                buffer[outIndex++] = (byte)((intSample >> 8) & 0xFF);
                buffer[outIndex++] = (byte)((intSample >> 16) & 0xFF);
            }
            return samplesRead * 3;
        }
    }
}
