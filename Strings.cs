// UI text in 14 languages. Column order matches LangCodes. Add a language: append a code, a native name,
// and one string to every row (Strings self-check: see test-strings.ps1).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

static partial class Program
{
    static readonly string[] LangCodes = { "en", "ar", "fr", "es", "de", "pt", "tr", "ru", "zh", "ja", "hi", "id", "fa", "ur" };
    static readonly string[] LangNames = { "English", "العربية", "Français", "Español", "Deutsch", "Português", "Türkçe", "Русский", "中文", "日本語", "हिन्दी", "Bahasa Indonesia", "فارسی", "اردو" };
    static readonly string[] LangEnglish = { "English", "Arabic", "French", "Spanish", "German", "Portuguese", "Turkish", "Russian", "Chinese", "Japanese", "Hindi", "Indonesian", "Persian", "Urdu" };

    const string AppVersion = "1.0.0";

    // Chosen at install, saved in the registry; first run defaults to the Windows display language.
    static string lang;
    static string Lang
    {
        get
        {
            if (lang != null) return lang;
            try { lang = Microsoft.Win32.Registry.GetValue(SettingsKey, "Lang", null) as string; } catch { }
            if (Array.IndexOf(LangCodes, lang) < 0) lang = DetectLang();
            return lang;
        }
        set { lang = value; }
    }

    static string DetectLang()
    {
        string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Array.IndexOf(LangCodes, code) >= 0 ? code : "en";
    }

    static string L(string key)
    {
        string[] row;
        if (!Text.TryGetValue(key, out row)) return key;
        int i = Array.IndexOf(LangCodes, Lang);
        return row[i < 0 ? 0 : i];
    }

    static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
    {
        //            en, ar, fr, es, de, pt, tr, ru, zh, ja, hi, id, fa, ur
        { "app", new[] { "Desktop Menu", "قائمة سطح المكتب", "Menu Bureau", "Menú del escritorio", "Desktop-Menü", "Menu da área de trabalho", "Masaüstü Menüsü", "Меню рабочего стола", "桌面菜单", "デスクトップ メニュー", "डेस्कटॉप मेनू", "Menu Desktop", "منوی دسکتاپ", "ڈیسک ٹاپ مینو" } },
        { "empty", new[] { "(empty)", "(فارغ)", "(vide)", "(vacío)", "(leer)", "(vazio)", "(boş)", "(пусто)", "（空）", "（空）", "(खाली)", "(kosong)", "(خالی)", "(خالی)" } },
        { "openFolder", new[] { "Open folder", "فتح المجلد", "Ouvrir le dossier", "Abrir carpeta", "Ordner öffnen", "Abrir pasta", "Klasörü aç", "Открыть папку", "打开文件夹", "フォルダーを開く", "फ़ोल्डर खोलें", "Buka folder", "باز کردن پوشه", "فولڈر کھولیں" } },
        { "showDesktop", new[] { "Show desktop", "إظهار سطح المكتب", "Afficher le bureau", "Mostrar escritorio", "Desktop anzeigen", "Mostrar área de trabalho", "Masaüstünü göster", "Показать рабочий стол", "显示桌面", "デスクトップを表示", "डेस्कटॉप दिखाएँ", "Tampilkan desktop", "نمایش دسکتاپ", "ڈیسک ٹاپ دکھائیں" } },
        { "restore", new[] { "Restore windows", "إرجاع النوافذ", "Restaurer les fenêtres", "Restaurar ventanas", "Fenster wiederherstellen", "Restaurar janelas", "Pencereleri geri getir", "Вернуть окна", "恢复窗口", "ウィンドウを元に戻す", "विंडो वापस लाएँ", "Pulihkan jendela", "بازگرداندن پنجره‌ها", "ونڈوز بحال کریں" } },
        { "openDesktop", new[] { "Open Desktop folder", "فتح سطح المكتب", "Ouvrir le dossier Bureau", "Abrir carpeta Escritorio", "Desktop-Ordner öffnen", "Abrir pasta da área de trabalho", "Masaüstü klasörünü aç", "Открыть папку рабочего стола", "打开桌面文件夹", "デスクトップ フォルダーを開く", "डेस्कटॉप फ़ोल्डर खोलें", "Buka folder Desktop", "باز کردن پوشه دسکتاپ", "ڈیسک ٹاپ فولڈر کھولیں" } },
        { "settings", new[] { "Settings", "الإعدادات", "Paramètres", "Configuración", "Einstellungen", "Configurações", "Ayarlar", "Настройки", "设置", "設定", "सेटिंग्स", "Pengaturan", "تنظیمات", "ترتیبات" } },
        { "changeIcon", new[] { "Change icon", "تغيير الأيقونة", "Changer l'icône", "Cambiar icono", "Symbol ändern", "Alterar ícone", "Simgeyi değiştir", "Сменить значок", "更换图标", "アイコンを変更", "आइकन बदलें", "Ganti ikon", "تغییر آیکون", "آئیکن تبدیل کریں" } },
        { "openIcons", new[] { "Open icons folder", "فتح مجلد الأيقونات", "Ouvrir le dossier des icônes", "Abrir carpeta de iconos", "Symbolordner öffnen", "Abrir pasta de ícones", "Simge klasörünü aç", "Открыть папку значков", "打开图标文件夹", "アイコン フォルダーを開く", "आइकन फ़ोल्डर खोलें", "Buka folder ikon", "باز کردن پوشه آیکون‌ها", "آئیکن فولڈر کھولیں" } },
        { "closeMenu", new[] { "Close the menu", "إغلاق القائمة", "Fermer le menu", "Cerrar el menú", "Menü schließen", "Fechar o menu", "Menüyü kapat", "Закрыть меню", "关闭菜单", "メニューを閉じる", "मेनू बंद करें", "Tutup menu", "بستن منو", "مینو بند کریں" } },
        { "uninstall", new[] { "Uninstall", "إلغاء التثبيت", "Désinstaller", "Desinstalar", "Deinstallieren", "Desinstalar", "Kaldır", "Удалить", "卸载", "アンインストール", "अनइंस्टॉल करें", "Copot pemasangan", "حذف نصب", "ان انسٹال کریں" } },
        { "pickImage", new[] { "Choose an image for the icon", "اختر صورة للأيقونة", "Choisissez une image pour l'icône", "Elige una imagen para el icono", "Bild für das Symbol wählen", "Escolha uma imagem para o ícone", "Simge için bir resim seçin", "Выберите изображение для значка", "选择图标图片", "アイコンの画像を選択", "आइकन के लिए चित्र चुनें", "Pilih gambar untuk ikon", "یک تصویر برای آیکون انتخاب کنید", "آئیکن کے لیے تصویر منتخب کریں" } },
        { "images", new[] { "Images", "صور", "Images", "Imágenes", "Bilder", "Imagens", "Resimler", "Изображения", "图片", "画像", "चित्र", "Gambar", "تصاویر", "تصاویر" } },
        { "uninstallTitle", new[] { "Uninstall Desktop Menu", "إلغاء تثبيت قائمة سطح المكتب", "Désinstaller Menu Bureau", "Desinstalar Menú del escritorio", "Desktop-Menü deinstallieren", "Desinstalar Menu da área de trabalho", "Masaüstü Menüsü'nü kaldır", "Удалить «Меню рабочего стола»", "卸载桌面菜单", "デスクトップ メニューをアンインストール", "डेस्कटॉप मेनू अनइंस्टॉल करें", "Copot Menu Desktop", "حذف نصب منوی دسکتاپ", "ڈیسک ٹاپ مینو ان انسٹال کریں" } },
        { "uninstallLine1", new[] { "The app and your custom icons will be removed", "سيتم حذف البرنامج وأيقوناتك الخاصة", "L'application et vos icônes personnalisées seront supprimées", "Se eliminarán la aplicación y tus iconos personalizados", "Die App und deine eigenen Symbole werden entfernt", "O app e seus ícones personalizados serão removidos", "Uygulama ve özel simgeleriniz kaldırılacak", "Приложение и ваши значки будут удалены", "将删除应用和你的自定义图标", "アプリとカスタム アイコンが削除されます", "ऐप और आपके कस्टम आइकन हटा दिए जाएँगे", "Aplikasi dan ikon kustom Anda akan dihapus", "برنامه و آیکون‌های شخصی شما حذف می‌شوند", "ایپ اور آپ کے اپنے آئیکن حذف ہو جائیں گے" } },
        { "uninstallLine2", new[] { "If pinned to the taskbar: right-click › Unpin", "إذا كان مثبّتاً في شريط المهام: كليك يمين ← إلغاء التثبيت", "Si épinglé à la barre des tâches : clic droit › Détacher", "Si está anclado a la barra de tareas: clic derecho › Desanclar", "Falls an die Taskleiste angeheftet: Rechtsklick › Lösen", "Se estiver fixado na barra de tarefas: clique direito › Desafixar", "Görev çubuğuna sabitlediyseniz: sağ tık › Sabitlemeyi kaldır", "Если закреплено на панели задач: правый клик › Открепить", "若已固定到任务栏：右键 › 取消固定", "タスクバーにピン留めした場合: 右クリック › ピン留めを外す", "अगर टास्कबार पर पिन है: राइट-क्लिक › अनपिन करें", "Jika disematkan di taskbar: klik kanan › Lepas sematan", "اگر به نوار وظیفه سنجاق شده: کلیک راست › برداشتن سنجاق", "اگر ٹاسک بار پر پن ہے: رائٹ کلک › ان پن کریں" } },
        { "installTitle", new[] { "Install Desktop Menu", "تثبيت قائمة سطح المكتب", "Installer Menu Bureau", "Instalar Menú del escritorio", "Desktop-Menü installieren", "Instalar Menu da área de trabalho", "Masaüstü Menüsü'nü yükle", "Установить «Меню рабочего стола»", "安装桌面菜单", "デスクトップ メニューをインストール", "डेस्कटॉप मेनू इंस्टॉल करें", "Pasang Menu Desktop", "نصب منوی دسکتاپ", "ڈیسک ٹاپ مینو انسٹال کریں" } },
        { "where", new[] { "Where should the menu icon go?", "أين تريد أيقونة القائمة؟", "Où placer l'icône du menu ?", "¿Dónde quieres el icono del menú?", "Wo soll das Menüsymbol erscheinen?", "Onde colocar o ícone do menu?", "Menü simgesi nerede olsun?", "Где разместить значок меню?", "菜单图标放在哪里？", "メニュー アイコンの場所", "मेनू आइकन कहाँ रखें?", "Di mana ikon menu ditempatkan?", "آیکون منو کجا باشد؟", "مینو آئیکن کہاں رکھیں؟" } },
        { "hidden", new[] { "Hidden icons", "قائمة الأيقونات المخفية", "Icônes masquées", "Iconos ocultos", "Ausgeblendete Symbole", "Ícones ocultos", "Gizli simgeler", "Скрытые значки", "隐藏的图标", "隠れているアイコン", "छिपे हुए आइकन", "Ikon tersembunyi", "آیکون‌های پنهان", "چھپے ہوئے آئیکن" } },
        { "hiddenSub", new[] { "Under the ^ arrow next to the clock", "تحت السهم ^ بجانب الساعة", "Sous la flèche ^ près de l'horloge", "Bajo la flecha ^ junto al reloj", "Unter dem Pfeil ^ neben der Uhr", "Sob a seta ^ ao lado do relógio", "Saatin yanındaki ^ okunun altında", "Под стрелкой ^ рядом с часами", "在时钟旁的 ^ 箭头下", "時計の横の ^ の中", "घड़ी के पास ^ तीर के नीचे", "Di bawah panah ^ dekat jam", "زیر فلش ^ کنار ساعت", "گھڑی کے پاس ^ تیر کے نیچے" } },
        { "clock", new[] { "Next to the clock", "بجانب الساعة", "À côté de l'horloge", "Junto al reloj", "Neben der Uhr", "Ao lado do relógio", "Saatin yanında", "Рядом с часами", "时钟旁边", "時計の横", "घड़ी के पास", "Di samping jam", "کنار ساعت", "گھڑی کے ساتھ" } },
        { "clockSub", new[] { "Always visible on the taskbar", "ظاهرة دائماً في شريط المهام", "Toujours visible dans la barre des tâches", "Siempre visible en la barra de tareas", "Immer sichtbar in der Taskleiste", "Sempre visível na barra de tarefas", "Görev çubuğunda her zaman görünür", "Всегда видно на панели задач", "始终显示在任务栏上", "タスクバーに常に表示", "टास्कबार पर हमेशा दिखे", "Selalu terlihat di taskbar", "همیشه در نوار وظیفه دیده می‌شود", "ٹاسک بار پر ہمیشہ نظر آئے" } },
        { "start", new[] { "Start menu", "قائمة ابدأ", "Menu Démarrer", "Menú Inicio", "Startmenü", "Menu Iniciar", "Başlat menüsü", "Меню «Пуск»", "开始菜单", "スタート メニュー", "स्टार्ट मेनू", "Menu Start", "منوی استارت", "اسٹارٹ مینو" } },
        { "startSub", new[] { "Pin it to the taskbar later from here", "ومنها تثبّته في شريط المهام لاحقاً", "Épinglez-le ensuite à la barre des tâches", "Desde ahí puedes anclarlo a la barra de tareas", "Von dort später an die Taskleiste anheften", "Depois fixe na barra de tarefas a partir dali", "Oradan görev çubuğuna sabitleyebilirsiniz", "Оттуда можно закрепить на панели задач", "之后可从这里固定到任务栏", "ここからタスクバーにピン留めできます", "यहीं से बाद में टास्कबार पर पिन करें", "Dari sini bisa disematkan ke taskbar", "بعداً از اینجا به نوار وظیفه سنجاق کنید", "یہاں سے بعد میں ٹاسک بار پر پن کریں" } },
        { "install", new[] { "Install", "تثبيت", "Installer", "Instalar", "Installieren", "Instalar", "Yükle", "Установить", "安装", "インストール", "इंस्टॉल करें", "Pasang", "نصب", "انسٹال کریں" } },
        { "doneTitle", new[] { "Installed successfully", "تم التثبيت بنجاح", "Installation réussie", "Instalado correctamente", "Erfolgreich installiert", "Instalado com sucesso", "Başarıyla yüklendi", "Установка завершена", "安装成功", "インストールが完了しました", "सफलतापूर्वक इंस्टॉल हुआ", "Berhasil dipasang", "با موفقیت نصب شد", "کامیابی سے انسٹال ہو گیا" } },
        { "doneClock", new[] { "The icon is next to the clock", "الأيقونة الآن بجانب الساعة", "L'icône est à côté de l'horloge", "El icono está junto al reloj", "Das Symbol ist neben der Uhr", "O ícone está ao lado do relógio", "Simge saatin yanında", "Значок рядом с часами", "图标在时钟旁边", "アイコンは時計の横にあります", "आइकन घड़ी के पास है", "Ikon ada di samping jam", "آیکون کنار ساعت است", "آئیکن گھڑی کے ساتھ ہے" } },
        { "doneHidden", new[] { "The icon is under the ^ arrow", "الأيقونة في قائمة الأيقونات المخفية ^", "L'icône est sous la flèche ^", "El icono está bajo la flecha ^", "Das Symbol ist unter dem Pfeil ^", "O ícone está sob a seta ^", "Simge ^ okunun altında", "Значок под стрелкой ^", "图标在 ^ 箭头下", "アイコンは ^ の中にあります", "आइकन ^ तीर के नीचे है", "Ikon ada di bawah panah ^", "آیکون زیر فلش ^ است", "آئیکن ^ تیر کے نیچے ہے" } },
        { "doneStart", new[] { "Find it in the Start menu", "تجده في قائمة ابدأ", "Retrouvez-le dans le menu Démarrer", "Búscalo en el menú Inicio", "Zu finden im Startmenü", "Encontre no menu Iniciar", "Başlat menüsünde bulabilirsiniz", "Ищите в меню «Пуск»", "可在开始菜单中找到", "スタート メニューにあります", "स्टार्ट मेनू में मिलेगा", "Temukan di menu Start", "در منوی استارت پیدایش کنید", "اسٹارٹ مینو میں ملے گا" } },
        { "doneAuto", new[] { "Starts automatically with Windows", "يشتغل تلقائياً مع تشغيل الجهاز", "Démarre automatiquement avec Windows", "Se inicia automáticamente con Windows", "Startet automatisch mit Windows", "Inicia automaticamente com o Windows", "Windows ile otomatik başlar", "Запускается вместе с Windows", "随 Windows 自动启动", "Windows と一緒に自動で起動します", "Windows के साथ अपने आप शुरू होता है", "Berjalan otomatis bersama Windows", "همراه ویندوز خودکار اجرا می‌شود", "Windows کے ساتھ خود بخود چلتا ہے" } },
        { "done", new[] { "Done", "تم", "Terminé", "Listo", "Fertig", "Concluir", "Tamam", "Готово", "完成", "完了", "हो गया", "Selesai", "تمام", "ہو گیا" } },
        { "failTitle", new[] { "Installation failed", "تعذّر التثبيت", "Échec de l'installation", "Error de instalación", "Installation fehlgeschlagen", "Falha na instalação", "Yükleme başarısız", "Не удалось установить", "安装失败", "インストールに失敗しました", "इंस्टॉल नहीं हो सका", "Pemasangan gagal", "نصب انجام نشد", "انسٹال نہیں ہو سکا" } },
        { "failHint", new[] { "Close the app if it is open and try again", "أغلق البرنامج إذا كان مفتوحاً وحاول مرة ثانية", "Fermez l'application si elle est ouverte et réessayez", "Cierra la aplicación si está abierta e inténtalo de nuevo", "Schließe die App, falls geöffnet, und versuche es erneut", "Feche o app se estiver aberto e tente de novo", "Uygulama açıksa kapatıp tekrar deneyin", "Закройте приложение, если оно открыто, и повторите", "如果应用已打开，请关闭后重试", "アプリが開いていれば閉じて、もう一度お試しください", "ऐप खुला हो तो बंद करके फिर कोशिश करें", "Tutup aplikasi jika terbuka lalu coba lagi", "اگر برنامه باز است ببندید و دوباره امتحان کنید", "ایپ کھلی ہو تو بند کر کے دوبارہ کوشش کریں" } },
        { "errTitle", new[] { "Something went wrong", "حدث خطأ", "Une erreur s'est produite", "Algo salió mal", "Etwas ist schiefgelaufen", "Algo deu errado", "Bir sorun oluştu", "Что-то пошло не так", "出错了", "問題が発生しました", "कुछ गलत हो गया", "Terjadi kesalahan", "خطایی رخ داد", "کچھ غلط ہو گیا" } },
        { "ok", new[] { "OK", "حسناً", "OK", "Aceptar", "OK", "OK", "Tamam", "ОК", "确定", "OK", "ठीक है", "OK", "باشه", "ٹھیک ہے" } },
        { "language", new[] { "Language", "اللغة", "Langue", "Idioma", "Sprache", "Idioma", "Dil", "Язык", "语言", "言語", "भाषा", "Bahasa", "زبان", "زبان" } },
        { "next", new[] { "Next", "التالي", "Suivant", "Siguiente", "Weiter", "Avançar", "İleri", "Далее", "下一步", "次へ", "आगे", "Lanjut", "بعدی", "اگلا" } },
        { "chooseLang", new[] { "Choose your language", "اختر لغتك", "Choisissez votre langue", "Elige tu idioma", "Sprache wählen", "Escolha seu idioma", "Dilinizi seçin", "Выберите язык", "选择语言", "言語を選択", "अपनी भाषा चुनें", "Pilih bahasa Anda", "زبان خود را انتخاب کنید", "اپنی زبان منتخب کریں" } },
    };
}
