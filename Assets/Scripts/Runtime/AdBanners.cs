namespace OnlyUp
{
    /// <summary>
    /// O'yin paytida ekranning ikki chetidagi bannerlar. Ularni web sahifa chizadi
    /// (Assets/WebGLTemplates/Tepaga), o'yin faqat holatni aytadi. Boshqa platformalarda hech narsa chiqmaydi.
    /// </summary>
    public static class AdBanners
    {
        public enum Mode
        {
            Hidden = 0,
            /// <summary>Ko'rinadi, lekin bosish va teginishlar o'yinga o'tib ketadi.</summary>
            Visible = 1,
            /// <summary>Pauzada: havolani bosish mumkin.</summary>
            Clickable = 2
        }

        /// <summary>Banner chiqadigan platformada o'ng tomondagi yozuvlar ichkariroq suriladi.</summary>
        public static bool Supported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static Mode Current { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void TepagaAdsSet(int mode);
#endif

        public static void Set(Mode mode)
        {
            if (mode == Current) return;
            Current = mode;
#if UNITY_WEBGL && !UNITY_EDITOR
            TepagaAdsSet((int)mode);
#endif
        }
    }
}
