mergeInto(LibraryManager.library, {
    ShowGoogleBanner: function () {
        // JS는 문자열을 반환하지 않아야 함

        // 실제 광고 DOM 삽입
	var ad = document.createElement('div');
        ad.className = "adsbygoogle";
        ad.style.display = "block";
        ad.style.position = 'fixed';
        ad.style.bottom = '0px';
        ad.style.left = '0px';
        ad.style.width = '100%';
        ad.style.height = '90px';
        ad.style.background = '#ccc';
        ad.textContent = "Google Ad Placeholder";

        ad.setAttribute("data-ad-client", "ca-pub-6960876675326082");
        ad.setAttribute("data-ad-slot", "8608991216");
        ad.setAttribute("data-ad-format", "auto");
        ad.setAttribute("data-full-width-responsive", "true");

        document.body.appendChild(ad);
        (adsbygoogle = window.adsbygoogle || []).push({});
    }
});