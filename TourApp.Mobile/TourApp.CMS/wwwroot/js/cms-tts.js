/**
 * Chọn giọng TTS trong trình duyệt (Web Speech API).
 * Tiếng Việt: ưu tiên giọng nữ địa phương (vd. Microsoft Hoai My) — không dùng giọng Nam / en-US đọc văn bản tiếng Việt.
 * Lý do hay nghe như “ông Tây”: trình duyệt chọn nhầm giọng tiếng Anh hoặc giọng nam (Nam Minh, …) khi danh sách voice tải muộn.
 * Cài thêm: Windows — Cài đặt → Thời gian và ngôn ngữ → Ngôn ngữ nói → thêm Tiếng Việt (Hoai My).
 */
(function () {
    function scoreVietnameseVoice(v) {
        var n = (v.name || '').toLowerCase();
        var l = (v.lang || '').toLowerCase();
        if (l !== 'vi-vn' && l.indexOf('vi') !== 0)
            return -999;
        var s = l === 'vi-vn' ? 30 : 15;
        if (/hoai\s*my|hoaimy|hoài/.test(v.name))
            s += 120;
        if (/\bmai\b|female|woman|\bnữ\b|natural.*vi|vietnam/.test(n))
            s += 45;
        if (/\blinh\b|\blan\b|\bly\b|\bnga\b|huong|hương/.test(n))
            s += 22;
        if (/\bnam\b|\bmale\b|nghiêm|nam\s*minh|namminh/.test(n))
            s -= 150;
        if (/english|en-us|en_gb|en-gb|\(us\)|\(uk\)|david|fred|google.*\sen\b|microsoft\s*mark|microsoft\s*george/.test(n) && !/vi/.test(l))
            s -= 120;
        return s;
    }

    window.cmsPickVietnameseFemaleVoice = function () {
        var voices = speechSynthesis.getVoices();
        if (!voices || !voices.length)
            return null;
        var vi = [];
        for (var i = 0; i < voices.length; i++) {
            var l = (voices[i].lang || '').toLowerCase();
            if (l === 'vi-vn' || l.indexOf('vi') === 0)
                vi.push(voices[i]);
        }
        if (!vi.length)
            return null;
        vi.sort(function (a, b) { return scoreVietnameseVoice(b) - scoreVietnameseVoice(a); });
        var best = vi[0];
        return scoreVietnameseVoice(best) > -80 ? best : null;
    };

    window.cmsPickTtsVoiceForLocale = function (locale, langCode) {
        var voices = speechSynthesis.getVoices();
        if (!voices || !voices.length)
            return null;
        var loc = locale || 'en-US';
        var shortLang = (langCode || loc.substring(0, 2) || 'en').toLowerCase();
        if (shortLang === 'vi') {
            var vn = window.cmsPickVietnameseFemaleVoice();
            if (vn)
                return vn;
        }
        var same = [];
        for (var j = 0; j < voices.length; j++) {
            if ((voices[j].lang || '').toLowerCase().indexOf(shortLang) === 0)
                same.push(voices[j]);
        }
        var hints = ['female', 'woman', 'zira'];
        for (var k = 0; k < same.length; k++) {
            var nm = same[k].name.toLowerCase();
            for (var h = 0; h < hints.length; h++) {
                if (nm.indexOf(hints[h]) >= 0)
                    return same[k];
            }
        }
        return same.length ? same[0] : null;
    };

    window.cmsWarmUpSpeechVoices = function () {
        try {
            speechSynthesis.getVoices();
        } catch (e) { /* ignore */ }
        speechSynthesis.onvoiceschanged = function () {
            try {
                speechSynthesis.getVoices();
            } catch (e2) { /* ignore */ }
        };
    };
})();
