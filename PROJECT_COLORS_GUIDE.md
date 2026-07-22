# راهنمای رنگ‌های پروژه سایرا (Sayra Project Colors Guide)

این سند شامل تمام رنگ‌های استفاده شده در بخش‌های مختلف پروژه **سایرا (Sayra)** است. این پروژه دارای دو رابط کاربری اصلی است:
1. **Sayra.UI:** نمونه اولیه مستقل و باکیفیت بالا (High-Fidelity Prototype) با طراحی مدرن، افکت‌های شیشه‌ای (Glassmorphism)، انیمیشن‌های جذاب و زبان فارسی.
2. **Sayra.Client.UI:** کلاینت نهایی متصل به هسته پس‌زمینه سیستم.

در ادامه تمام پالت‌های رنگی به تفکیک بخش‌ها، به همراه کد HEX، کلید منبع (Resource Key) در کد XAML و کاربرد هر کدام آورده شده است.

---

## فهرست بخش‌ها
1. [رنگ‌های پایه و مشترک (Base & Global Colors)](#1-رنگ‌های-پایه-و-مشترک-base--global-colors)
2. [صفحه اصلی / داشبورد کاربر (Home Page / Dashboard)](#2-صفحه-اصلی--داشبورد-کاربر-home-page--dashboard)
3. [صفحه ورود و احراز هویت (Login Screen)](#3-صفحه-ورود-و-احراز-هویت-login-screen)
4. [پنل مدیریت (Admin Dashboard)](#4-پنل-مدیریت-admin-dashboard)
   - [رنگ‌های پس‌زمینه و سطوح (Background & Surface)](#رنگ‌های-پس‌زمینه-و-سطوح-background--surface)
   - [دکمه‌ها و فیلدها (Buttons & Fields)](#دکمه‌ها-و-فیلدها-buttons--fields)
   - [تراکسپوزهای نیمه‌شفاف (Translucent Overlays)](#تراکسپوزهای-نیمه‌شفاف-translucent-overlays)
   - [رنگ‌های اعلانات و حالت‌ها (States & Alerts Accents)](#رنگ‌های-اعلانات-و-حالت‌ها-states--alerts-accents)
5. [رنگ‌های کلاینت اصلی (Sayra.Client.UI Colors)](#5-رنگ‌های-کلاینت-اصلی-sayraclientui-colors)

---

### ۱. رنگ‌های پایه و مشترک (Base & Global Colors)
این رنگ‌ها به عنوان تم کلی و توکن‌های طراحی پایه در فایل `Sayra.UI/Resources/Colors.xaml` تعریف شده‌اند.

| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/101014/000000?text=+" width="15" height="15"> | `#101014` | `Background` / `BackgroundColor` / `BodyColor` | رنگ پس‌زمینه عمیق و تاریک کلی نرم‌افزار |
| <img src="https://via.placeholder.com/15/ffff3d/000000?text=+" width="15" height="15"> | `#ffff3d` | `Primary` / `PrimaryYellowColor` / `AccentYellowColor` | رنگ زرد نئونی امضا و برند اصلی سایرا |
| <img src="https://via.placeholder.com/15/f4f46b/000000?text=+" width="15" height="15"> | `#f4f46b` | `PrimaryHover` / `PrimaryHoverYellowColor` | رنگ زرد روشن‌تر هنگام نگه داشتن موس (Hover) روی دکمه‌ها |
| <img src="https://via.placeholder.com/15/1F1F23/000000?text=+" width="15" height="15"> | `#1F1F23` | `SecondaryBackground` / `SecondaryBackgroundColor` / `SurfaceColor` / `CardColor` | رنگ سطوح کارت‌ها، کامپوننت‌ها و بخش‌های ثانویه |
| <img src="https://via.placeholder.com/15/252528/000000?text=+" width="15" height="15"> | `#252528` | `Border` / `BorderColor` | رنگ مرزها و خطوط جداکننده المان‌ها |
| <img src="https://via.placeholder.com/15/ffffff/000000?text=+" width="15" height="15"> | `#ffffff` | `White` / `WhiteColor` / `PrimaryTextColor` | رنگ سفید خالص برای متون اصلی و آیکون‌های روشن |
| <img src="https://via.placeholder.com/15/E1E1E6/000000?text=+" width="15" height="15"> | `#E1E1E6` | `DarkerWhite` | رنگ سفید مات مایل به خاکستری برای متون با اهمیت متوسط |
| <img src="https://via.placeholder.com/15/9A9A9A/000000?text=+" width="15" height="15"> | `#9A9A9A` | `Muted` / `SecondaryWhiteColor` / `MutedColor` / `SecondaryTextColor` | رنگ خاکستری تیره برای متون فرعی، کم‌اهمیت و توضیحات |
| <img src="https://via.placeholder.com/15/F46B6B/000000?text=+" width="15" height="15"> | `#F46B6B` | `Red` / `RedColor` / `DangerRedColor` | رنگ قرمز برای دکمه‌های خطر، حذف و خطاها |
| <img src="https://via.placeholder.com/15/14BE78/000000?text=+" width="15" height="15"> | `#14BE78` | `Success` / `SuccessColor` / `SuccessGreenColor` | رنگ سبز برای نمایش حالت‌های موفقیت، تایید و شروع |
| <img src="https://via.placeholder.com/15/EF4444/000000?text=+" width="15" height="15"> | `#EF4444` | `ErrorColor` | رنگ قرمز آلبالویی برای خطاهای سیستمی شدید |
| <img src="https://via.placeholder.com/15/E5A000/000000?text=+" width="15" height="15"> | `#E5A000` | `YellowColor` | رنگ طلایی/نارنجی برای هشدارها و حالت‌های معلق |

همچنین توکن‌های جدید فاز ۱ در `Sayra.UI/Themes/Colors.xaml` به شرح زیر است:
- `App.Background` -> `#101014`
- `App.Surface` -> `#1F1F23`
- `App.Primary` -> `#AA0072` (رنگ ارغوانی برندینگ جدید)
- `App.Accent` -> `#87F4F6` (رنگ آبی فیروزه‌ای نئونی)
- `App.Border` -> `#252528`
- `App.Text.Primary` -> `#FFFFFF`
- `App.Text.Secondary` -> `#9A9A9A`
- `App.Success` -> `#14BE78`
- `App.Error` -> `#EF4444`
- `App.Warning` -> `#E5A000`
- `App.Danger` -> `#F46B6B`

---

### ۲. صفحه اصلی / داشبورد کاربر (Home Page / Dashboard)
این رنگ‌ها به طور ویژه برای رابط کاربری گیمرها (خانه) طراحی شده‌اند تا ظاهری لوکس و جذاب را القا کنند (`Sayra.UI/Themes/Colors.xaml`).

| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/08090D/000000?text=+" width="15" height="15"> | `#08090D` | `Home.Background.Color` | پس‌زمینه بسیار تیره و عمیق صفحه خانه (جایگزین بهینه ویدیوهای سنگین) |
| <img src="https://via.placeholder.com/15/F5FF00/000000?text=+" width="15" height="15"> | `#F5FF00` | `Home.AccentYellow.Color` | زرد نئونی براق برای هایلایت‌ها، تایمرها و دکمه‌های اصلی خانه |
| <img src="https://via.placeholder.com/15/1AF46B6B/000000?text=+" width="15" height="15"> | `#1AF46B6B` | `Home.EndSession.HoverBg.Color` | قرمز بسیار شفاف (با غلظت ۱۰٪) برای هاور دکمه خروج از حساب |
| <img src="https://via.placeholder.com/15/33F46B6B/000000?text=+" width="15" height="15"> | `#33F46B6B` | `Home.EndSession.PressedBg.Color` | قرمز نیمه شفاف (با غلظت ۲۰٪) برای کلیک دکمه خروج از حساب |

---

### ۳. صفحه ورود و احراز هویت (Login Screen)
رنگ‌های مربوط به صفحه لاگین که بر پایه طراحی شیشه‌نمایی (Glassmorphism) توسعه یافته‌اند (`Sayra.UI/Themes/Colors.xaml`).

| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/F5FF00/000000?text=+" width="15" height="15"> | `#F5FF00` | `App.Login.YellowAccent` | رنگ زرد نئونی شاخص لاگین جهت ایجاد افکت Glow و فوکوس |
| <img src="https://via.placeholder.com/15/29F5FF00/000000?text=+" width="15" height="15"> | `#29F5FF00` | `App.Login.YellowBorder` | حاشیه زرد نئونی با شفافیت کم برای ایجاد عمق |
| <img src="https://via.placeholder.com/15/1A0A0A0A/000000?text=+" width="15" height="15"> | `#1A0A0A0A` | `App.Login.GlassInputBg` | پس‌زمینه شیشه‌ای بسیار تاریک فیلدهای ورودی |
| <img src="https://via.placeholder.com/15/22F5FF00/000000?text=+" width="15" height="15"> | `#22F5FF00` | `App.Login.GlassInputBorder` | حاشیه شیشه‌ای زرد نئونی فیلدهای ورودی |
| <img src="https://via.placeholder.com/15/A60A0A0A/000000?text=+" width="15" height="15"> | `#A60A0A0A` | `App.Login.GlassPanelBg` | پس‌زمینه پنل اصلی لاگین با حالت نیمه‌شفاف شیشه‌ای با غلظت بالا |
| <img src="https://via.placeholder.com/15/050507/000000?text=+" width="15" height="15"> | `#050507` | `App.Login.DarkBg` | رنگ پس‌زمینه تیره مطلق پشت پنل لاگین |
| <img src="https://via.placeholder.com/15/00050507/000000?text=+" width="15" height="15"> | `#00050507` | `App.Login.DarkBgTransparent` | پس‌زمینه کاملاً شفاف صفحه ورود برای انیمیشن‌های نرم |
| <img src="https://via.placeholder.com/15/1AFFFFFF/000000?text=+" width="15" height="15"> | `#1AFFFFFF` | `App.Login.WhiteBorder` / `WhiteHoverBg` | مرزها یا پس‌زمینه هاور شیشه‌ای سفید کم‌رنگ |
| <img src="https://via.placeholder.com/15/4DFFFFFF/000000?text=+" width="15" height="15"> | `#4DFFFFFF` | `App.Login.WhiteHoverBorder` | مرز شیشه‌ای سفید در حالت هاور با ۳۰٪ شفافیت |
| <img src="https://via.placeholder.com/15/33FFFFFF/000000?text=+" width="15" height="15"> | `#33FFFFFF` | `App.Login.WhitePressedBg` | پس‌زمینه دکمه‌ها در حالت کلیک شده (۲۰٪ شفافیت) |
| <img src="https://via.placeholder.com/15/66FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.4)` | `App.Login.PlaceholderText` | رنگ سفید نیمه‌شفاف برای متون راهنما (Placeholder) فیلدها |
| <img src="https://via.placeholder.com/15/A1A1AA/000000?text=+" width="15" height="15"> | `#A1A1AA` | `App.Login.SecondaryText` | رنگ خاکستری ملایم برای متون فرعی لاگین |

---

### ۴. پنل مدیریت (Admin Dashboard)
داشبورد مدیریت دارای پالت رنگی حرفه‌ای، سرد و با فوکوس بالا است. رنگ شاخص مدیریت بر خلاف خانه، آبی فیروزه‌ای نئونی (Cyan Accent) می‌باشد.

#### رنگ‌های پس‌زمینه و سطوح (Background & Surface)
| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/0F1014/000000?text=+" width="15" height="15"> | `#0F1014` | `Admin.Background.Color` | پس‌زمینه اصلی کل داشبورد مدیریت |
| <img src="https://via.placeholder.com/15/181A20/000000?text=+" width="15" height="15"> | `#181A20` | `Admin.Surface.Color` | پس‌زمینه کارت‌ها، لیست دسته‌بندی و جداول اطلاعات |
| <img src="https://via.placeholder.com/15/2A2D36/000000?text=+" width="15" height="15"> | `#2A2D36` | `Admin.Border.Color` | خطوط حاشیه و جداکننده‌ها در پنل مدیریت |
| <img src="https://via.placeholder.com/15/14161C/000000?text=+" width="15" height="15"> | `#14161C` | `Admin.RightSidebar.Bg.Color` | پس‌زمینه منوی سمت راست و ستون کناری |
| <img src="https://via.placeholder.com/15/1B1B1F/000000?text=+" width="15" height="15"> | `#1B1B1F` | `Admin.Modal.Bg.Start.Color` | نقطه شروع گرادینت پس‌زمینه مودال‌ها (پنجره‌های بازشو) |
| <img src="https://via.placeholder.com/15/101014/000000?text=+" width="15" height="15"> | `#101014` | `Admin.Modal.Bg.End.Color` | نقطه پایان گرادینت پس‌زمینه مودال‌ها |
| <img src="https://via.placeholder.com/15/252528/000000?text=+" width="15" height="15"> | `#252528` | `Admin.Modal.Border.Color` | حاشیه دور تا دور پنجره‌های بازشوی مدیریت |
| <img src="https://via.placeholder.com/15/121216/000000?text=+" width="15" height="15"> | `#121216` | `Admin.Details.CardBg.Color` | پس‌زمینه کارت‌های جزئیات و تب‌های داخلی مدیریت |

#### دکمه‌ها و فیلدها (Buttons & Fields)
| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/1E2026/000000?text=+" width="15" height="15"> | `#1E2026` | `Admin.Button.Bg.Color` | پس‌زمینه دکمه‌های معمولی تولبار و فرم‌ها |
| <img src="https://via.placeholder.com/15/E1E1E6/000000?text=+" width="15" height="15"> | `#E1E1E6` | `Admin.Button.Fg.Color` | رنگ متن و محتوای دکمه‌ها |
| <img src="https://via.placeholder.com/15/262933/000000?text=+" width="15" height="15"> | `#262933` | `Admin.Button.HoverBg.Color` | رنگ دکمه در زمان هاور (Hover) |
| <img src="https://via.placeholder.com/15/101215/000000?text=+" width="15" height="15"> | `#101215` | `Admin.Button.PressedBg.Color` | رنگ دکمه در زمان فشردن (Click) |
| <img src="https://via.placeholder.com/15/1C1F26/000000?text=+" width="15" height="15"> | `#1C1F26` | `Admin.Field.Bg.Color` / `Admin.Loading.PanelBg.Color` | پس‌زمینه ورودی‌ها (TextBox) و پنل لودینگ اطلاعات |
| <img src="https://via.placeholder.com/15/15171C/000000?text=+" width="15" height="15"> | `#15171C` | `Admin.Field.FocusedBg.Color` | پس‌زمینه ورودی‌ها در زمان فوکوس و تایپ |
| <img src="https://via.placeholder.com/15/252A34/000000?text=+" width="15" height="15"> | `#252A34` | `Admin.Selection.Bg.Color` | پس‌زمینه آیتم انتخاب شده در لیست‌ها و هاور سطرها |
| <img src="https://via.placeholder.com/15/2D3440/000000?text=+" width="15" height="15"> | `#2D3440` | `Admin.RowSelection.Bg.Color` | رنگ سطر انتخاب شده در جدول اصلی بازی‌ها |
| <img src="https://via.placeholder.com/15/1A1212/000000?text=+" width="15" height="15"> | `#1A1212` | `Admin.DangerZone.Bg.Color` | پس‌زمینه بخش‌های حساس مانند بخش حذف بازی (Danger Zone) |

#### تراکسپوزهای نیمه‌شفاف (Translucent Overlays)
این طیف برای افکت‌های شیشه‌ای نرم، اورلی‌های لودینگ یا سایه المان‌ها استفاده می‌شود.

| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/73000000/000000?text=+" width="15" height="15"> | `rgba(0,0,0,0.45)` | `Admin.Modal.Overlay.Color` | تیره کردن پس‌زمینه صفحه هنگام باز شدن مودال‌ها (۴۵٪ شفافیت) |
| <img src="https://via.placeholder.com/15/88000000/000000?text=+" width="15" height="15"> | `rgba(0,0,0,0.53)` | `Admin.Loading.Overlay.Color` | کاور لودینگ تیره هنگام دریافت اطلاعات (۵۳٪ شفافیت) |
| <img src="https://via.placeholder.com/15/06FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.02)` | `Admin.Translucent.06.Color` | لایه سفید بسیار شفاف ۲٪ برای پس‌زمینه المان‌های شناور |
| <img src="https://via.placeholder.com/15/12FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.07)` | `Admin.Translucent.12.Color` | لایه سفید شفاف ۷٪ برای هاور المان‌های فرعی |
| <img src="https://via.placeholder.com/15/13FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.075)`| `Admin.Translucent.13.Color` | لایه شیشه‌ای ظریف ۷.۵٪ |
| <img src="https://via.placeholder.com/15/22FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.13)` | `Admin.Translucent.22.Color` | لایه شیشه‌ای ۱۳٪ برای فیلدهای غیرفعال |
| <img src="https://via.placeholder.com/15/33FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.2)`  | `Admin.Translucent.33.Color` | لایه شیشه‌ای ۲۰٪ برای خطوط راهنما |
| <img src="https://via.placeholder.com/15/4DFFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.3)`  | `Admin.Translucent.4D.Color` | مرز المان‌ها در حالت عادی (۳۰٪ شفافیت) |
| <img src="https://via.placeholder.com/15/55FFFFFF/000000?text=+" width="15" height="15"> | `rgba(255,255,255,0.33)` | `Admin.Translucent.55.Color` | مرز المان‌ها در حالت فعال یا هاور (۳۳٪ شفافیت) |

#### رنگ‌های اعلانات و حالت‌ها (States & Alerts Accents)
مدیریت از چندین رنگ متمایز جهت نمایش وضعیت‌ها (موفقیت، خطا، هشدار، لودینگ، سیستم) استفاده می‌کند.

##### ۱. رنگ اصلی امضا (Cyan Accent)
آبی آسمانی فیروزه‌ای براق به عنوان هویت اصلی پنل مدیریت.
* **رنگ خالص:** `#63E6FF` (`Admin.Accent.Color`) - فوکوس ورودی‌ها، آیکون‌های فعال، اسکرول‌بار مدیریت
* **رنگ فرعی خالص:** `#00FFFF` (`Admin.Cyan.Color`) - وضعیت‌های زنده و پروسس‌های متصل
* **تراکسپوزها:**
  * `Admin.CellSelection.Bg.Color` -> `#2663E6FF` (طیف بسیار ملایم جهت انتخاب سلول جدول)
  * `Admin.ScrollThumb.Color` -> `#4063E6FF` (رنگ دسته اسکرول‌بار)
  * `Admin.Field.HoverBorder.Color` -> `#4463E6FF` (هاور مرز فیلدها)
  * `Admin.Cyan.Translucent.22.Color` -> `#2200FFFF` (۱۳٪ شفافیت)
  * `Admin.Cyan.Translucent.33.Color` -> `#3300FFFF` (۲۰٪ شفافیت)
  * `Admin.Cyan.Translucent.44.Color` -> `#4400FFFF` (۲۷٪ شفافیت)

##### ۲. رنگ موفقیت (Success Green)
سبز زمردی برای تسک‌های موفق و وضعیت‌های اوکی.
* **رنگ خالص:** `#14BE78` (`Admin.Success.Color`)
* **تراکسپوزها:**
  * `Admin.Success.Translucent.11.Color` -> `#1114BE78`
  * `Admin.Success.Translucent.22.Color` -> `#2214BE78`
  * `Admin.Success.Translucent.33.Color` -> `#3314BE78`
  * `Admin.Success.Translucent.44.Color` -> `#4414BE78`

##### ۳. رنگ خطا و حذف (Danger Red)
قرمز مرجانی برای هشدارها، خطاها و دکمه‌های بستن یا حذف.
* **رنگ خالص:** `#F46B6B` (`Admin.Danger.Color`)
* **دکمه بستن (هاور):** `#D9383A` (`Admin.CloseButton.HoverBg.Color`) - قرمز تیره جیغ
* **تراکسپوزها:**
  * `Admin.Danger.Translucent.22.Color` -> `#22F46B6B`
  * `Admin.Danger.Translucent.33.Color` -> `#33F46B6B`
  * `Admin.Danger.Translucent.44.Color` -> `#44F46B6B`

##### ۴. رنگ هشدار و زرد نئون (Neon Yellow)
زرد نئونی برای اخطارها، تداخل‌ها و آیتم‌های معلق.
* **رنگ خالص:** `#ffff3d` (`Admin.Yellow.Color`)
* **تراکسپوزها:**
  * `Admin.Yellow.Translucent.22.Color` -> `#22ffff3d`
  * `Admin.Yellow.Translucent.44.Color` -> `#44ffff3d`

##### ۵. رنگ سیستم و ارغوانی (Purple Accent)
بنفش سلطنتی برای دسته‌بندی ابزارهای سیستمی و شخصی‌سازی.
* **رنگ خالص:** `#8A5CFF` (`Admin.Purple.Color`)
* **تراکسپوزها:**
  * `Admin.Purple.Translucent.22.Color` -> `#228A5CFF`
  * `Admin.Purple.Translucent.44.Color` -> `#448A5CFF`

---

### ۵. رنگ‌های کلاینت اصلی (Sayra.Client.UI Colors)
برنامه کلاینت اصلی (`Sayra.Client.UI`) دارای تم تاریک بسیار شیک، مینیمال و یکپارچه است که در فایل `Sayra.Client.UI/Styles/Colors.xaml` تعریف شده است.

| نمایش رنگ | کد HEX | کلید منبع (Resource Key) | کاربرد و توضیحات |
| :---: | :---: | :--- | :--- |
| <img src="https://via.placeholder.com/15/0D0D12/000000?text=+" width="15" height="15"> | `#0D0D12` | `AppBackgroundColor` | رنگ پس‌زمینه تیره اصلی کل کلاینت |
| <img src="https://via.placeholder.com/15/181820/000000?text=+" width="15" height="15"> | `#181820` | `DarkSurfaceColor` | رنگ سطوح و پنل‌های تیره کلاینت |
| <img src="https://via.placeholder.com/15/050507/000000?text=+" width="15" height="15"> | `#050507` | `CardSurfaceColor` | رنگ پس‌زمینه کارت‌های نمایش بازی و اطلاعات |
| <img src="https://via.placeholder.com/15/F5FF00/000000?text=+" width="15" height="15"> | `#F5FF00` | `PrimaryAccentColor` | رنگ زرد نئونی جذاب به عنوان رنگ هویت بصری اصلی کلاینت |
| <img src="https://via.placeholder.com/15/22C55E/000000?text=+" width="15" height="15"> | `#22C55E` | `SuccessColor` | رنگ سبز برای اعلانات موفقیت آمیز و زمان فعال |
| <img src="https://via.placeholder.com/15/EF4444/000000?text=+" width="15" height="15"> | `#EF4444` | `DangerColor` | رنگ قرمز برای ارورها، هشدارهای اتمام وقت و دکمه‌های بحرانی |
| <img src="https://via.placeholder.com/15/FFFFFF/000000?text=+" width="15" height="15"> | `#FFFFFF` | `PrimaryTextColor` | رنگ سفید درخشان برای تمامی متون اصلی کلاینت |
| <img src="https://via.placeholder.com/15/A1A1AA/000000?text=+" width="15" height="15"> | `#A1A1AA` | `SecondaryTextColor` | رنگ نقره‌ای/خاکستری ملایم برای متون فرعی و راهنما |

---
*سند حاضر مرجع معتبر رنگ‌های استفاده شده در کدهای رابط کاربری پروژه سایرا بوده و تمامی استایل‌ها و پوسته‌ها از همین مقادیر بهره می‌برند.*
