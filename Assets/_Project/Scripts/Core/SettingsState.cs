namespace HellpitRampage.Core
{
    public class SettingsState
    {
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
        public int TargetFramerate = -1;

        // Accessibility
        public int ScreenShakeIntensity = 2; // 0=Off, 1=Low, 2=Normal
        public bool ReduceMotion = false;
        public bool HighContrast = false;

        public static SettingsState FromSaveData(SettingsSaveData d)
        {
            return new SettingsState
            {
                MasterVolume = d.MasterVolume,
                MusicVolume = d.MusicVolume,
                SfxVolume = d.SfxVolume,
                VoiceVolume = d.VoiceVolume,
                MuteMaster = d.MuteMaster,
                MuteMusic = d.MuteMusic,
                MuteSfx = d.MuteSfx,
                MuteVoice = d.MuteVoice,
                ResolutionWidth = d.ResolutionWidth,
                ResolutionHeight = d.ResolutionHeight,
                FullScreenMode = d.FullScreenMode,
                VSync = d.VSync,
                TargetFramerate = d.TargetFramerate,
                ScreenShakeIntensity = d.ScreenShakeIntensity,
                ReduceMotion = d.ReduceMotion,
                HighContrast = d.HighContrast,
            };
        }

        public SettingsSaveData ToSaveData()
        {
            return new SettingsSaveData
            {
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                SfxVolume = SfxVolume,
                VoiceVolume = VoiceVolume,
                MuteMaster = MuteMaster,
                MuteMusic = MuteMusic,
                MuteSfx = MuteSfx,
                MuteVoice = MuteVoice,
                ResolutionWidth = ResolutionWidth,
                ResolutionHeight = ResolutionHeight,
                FullScreenMode = FullScreenMode,
                VSync = VSync,
                TargetFramerate = TargetFramerate,
                ScreenShakeIntensity = ScreenShakeIntensity,
                ReduceMotion = ReduceMotion,
                HighContrast = HighContrast,
            };
        }
    }
}
