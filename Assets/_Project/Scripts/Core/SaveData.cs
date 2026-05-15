using System;

namespace HellpitRampage.Core
{
    [Serializable]
    public class SaveData
    {
        public int SchemaVersion = 1;
        public PlayerProfile Profile = new PlayerProfile();
        public MetaProgress Meta = new MetaProgress();
        public CurrentRun Run = null;
    }

    [Serializable]
    public class PlayerProfile
    {
    }

    [Serializable]
    public class MetaProgress
    {
    }

    [Serializable]
    public class CurrentRun
    {
    }

    [Serializable]
    public class SettingsSaveData
    {
        public int SaveFormatVersion = 1;

        // Audio
        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.85f;
        public float VoiceVolume = 0.9f;
        public bool MuteMaster = false;
        public bool MuteMusic = false;
        public bool MuteSfx = false;
        public bool MuteVoice = false;

        // Display
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public int FullScreenMode = 0; // 0=Fullscreen Window, 1=Exclusive Fullscreen, 2=Windowed
        public bool VSync = true;
        public int TargetFramerate = -1; // -1 = uncapped

        // Accessibility
        public int ScreenShakeIntensity = 2; // 0=Off, 1=Low, 2=Normal
        public bool ReduceMotion = false;
        public bool HighContrast = false;
    }
}
