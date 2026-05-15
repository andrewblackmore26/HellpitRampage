namespace HellpitRampage.Core
{
    public enum SettingKind
    {
        MasterVolume,
        MusicVolume,
        SfxVolume,
        VoiceVolume,
        MuteMaster,
        MuteMusic,
        MuteSfx,
        MuteVoice,
        Resolution,
        VSync,
        Framerate,
        ScreenShake,
        ReduceMotion,
        HighContrast,
    }

    public struct SettingsChangedEvent : IGameEvent
    {
        public SettingKind Kind;
    }
}
