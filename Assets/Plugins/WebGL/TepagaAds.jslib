mergeInto(LibraryManager.library, {
  // Web sahifadagi yon bannerlarni boshqaradi: 0 - yashirin, 1 - ko'rinadi, 2 - bosiladi.
  TepagaAdsSet: function (mode) {
    if (typeof window.tepagaAds === "function") window.tepagaAds(mode);
  },
});
