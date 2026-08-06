# کلاینت سایرا (SAYRA Client)

[![SAYRA System](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com)
[![Build Status](https://img.shields.io/badge/.NET-8.0-blueviolet.svg)](https://dotnet.microsoft.com/download)
[![Security](https://img.shields.io/badge/Security-SQLCipher%20%7C%20AES--256%20%7C%20ECDsa-red.svg)]()
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD-brightgreen.svg)]()

کلاینت سایرا (SAYRA Client) یک سرویس سیستمی (Windows Service) فوق‌پیشرفته، با امنیت نظامی (Military-Grade) و آماده برای محیط‌های عملیاتی بزرگ (Enterprise) است که به عنوان جزء کلاینت در سیستم مدیریت یکپارچه کافی‌نت‌ها، گیم‌نت‌ها و ایستگاه‌های بازی (Cyber Cafe & Gaming Station Management) عمل می‌کند. این پروژه با معماری تمیز (Clean Architecture) و مبتنی بر دامنه (DDD) با استفاده از زبان سی‌شارپ و فریم‌ورک .NET 8 پیاده‌سازی شده است و تمامی فرآیندها بدون نیاز به رابط کاربری مزاحم در پس‌زمینه سیستم اجرا می‌شوند.

SAYRA Client is an ultra-advanced, military-grade, production-ready Windows Service designed as the client component of the SAYRA GameNet, Cyber Cafe, and Gaming Station management suite. Built on .NET 8 using Clean Architecture and Domain-Driven Design (DDD) principles, it operates natively in the background as a secure, high-resilience, low-footprint daemon.

---

## فهرست مطالب / Table of Contents
1. [ویژگی‌های کلیدی و معماری سیستم (Key Features & Architecture)](#ویژگیهای-کلیدی-و-معماری-سیستم-key-features--architecture)
2. [مرور فنی ماژول‌های پیاده‌سازی شده (Technical Overview of Implemented Modules)](#مرور-فنی-ماژولهای-پیادهسازی-شده-technical-overview-of-implemented-modules)
   - [۱. سیستم امنیتی و ارتباطات امن (Secure IPC & Crypto)](#۱-سیستم-امنیتی-و-ارتباطات-امن-secure-ipc--crypto)
   - [۲. موتور اجرای امن بازی و ایزولاسیون (Secure Launch & Sandbox Isolation)](#۲-موتور-اجرای-امن-بازی-و-ایزولاسیون-secure-launch--sandbox-isolation)
   - [۳. پلتفرم پویای بروزرسانی سازمانی (Enterprise Update Platform)](#۳-پلتفرم-پویای-بروزرسانی-سازمانی-enterprise-update-platform)
   - [۴. موتور خودترمیمی، سلامت و انعطاف‌پذیری (Resilience, Health & Self-Healing)](#۴-موتور-خودترمیمی-سلامت-و-انعطافپذیری-resilience-health--self-healing)
   - [۵. پلتفرم پایش، ردیابی و تلمتری (Observability, Telemetry & Tracing)](#۵-پلتفرم-پایش-ردیابی-و-تلمتری-observability-telemetry--tracing)
   - [۶. موتور مدیریت ناوگان و عملیات راه دور (Enterprise Fleet Management & Remote Ops)](#۶-موتور-مدیریت-ناوگان-و-عملیات-راه-دور-enterprise-fleet-management--remote-ops)
3. [نحوه راه‌اندازی و توسعه (Installation & Development)](#نحوه-راهاندازی-و-توسعه-installation--development)
4. [امنیت و پایبندی به استانداردها (Security & Compliance)](#امنیت-و-پایبندی-به-استانداردها-security--compliance)

---

## ویژگی‌های کلیدی و معماری سیستم (Key Features & Architecture)

کلاینت سایرا بر اساس الگوهای طراحی پیشرفته طراحی شده است که پایداری ۱۰۰٪ سیستم و محافظت کامل در برابر تقلب، دستکاری و نفوذ را تضمین می‌کند.

* **معماری ماژولار و تمیز (Clean Architecture):** تفکیک کامل لایه‌های دامنه (Domain)، اپلیکیشن (Application)، زیرساخت (Infrastructure) و ارائه (Presentation) جهت توسعه‌پذیری حداکثری.
* **تاب‌آوری بالا (High Resilience):** استفاده از الگوهای خودترمیمی (Self-Healing)، مدیریت صف‌های اولویت‌دار، و بازیابی هوشمند در هنگام کراش برای بازگردانی سرویس‌ها به حالت عادی بدون دخالت مدیر سیستم.
* **اجرای با کمترین مصرف منابع (Zero-Footprint Execution):** بهینه‌سازی شده برای اجرای پس‌زمینه با کمترین میزان مصرف حافظه (RAM) و پردازنده (CPU) جهت جلوگیری از هرگونه تداخل با فریم‌ریت بازی‌ها.
* **رابط کاربری بومی‌سازی شده بازی‌ها:** پشتیبانی کامل از جهت‌دهی راست‌به‌چپ (RTL) به زبان فارسی، تایپوگرافی اختصاصی («تازی ها» با فونت Peyda Bold) و تم تاریک مدرن در لایه نمایش.

---

## مرور فنی ماژول‌های پیاده‌سازی شده (Technical Overview of Implemented Modules)

### ۱. سیستم امنیتی و ارتباطات امن (Secure IPC & Crypto)
* **پایگاه داده رمزگذاری‌شده SQLCipher:** تمامی پایگاه‌های داده محلی (شامل صف‌های آفلاین، لاگ‌های ردیابی، دارایی‌ها و تاریخچه سیستم) با کلید‌های ۲۵۶ بیتی منحصربه‌فرد که توسط Windows DPAPI محافظت می‌شوند، از طریق پروتکل سخت‌گیرانه SQLCipher رمزگذاری شده‌اند.
* **امنیت ارتباطات بین‌پروسسی (Secure IPC):** به کارگیری لوله نام‌گذاری شده (Named Pipe) با DACL‌های فوق‌العاده سخت‌گیرانه که دسترسی را فقط به حساب‌های کاربری SYSTEM، مدیران و کاربر فعال کنسول (`InteractiveSid`) محدود کرده و جلوی حملات جعل هویت را می‌گیرد.
* **مکانیسم‌های ضد اجرای مجدد (Anti-Replay):** اعتبارسنجی بازه زمانی پیام‌ها (Time-skew) و توکن‌های یکبار مصرف جهت خنثی‌سازی حملات Replay.

### ۲. موتور اجرای امن بازی و ایزولاسیون (Secure Launch & Sandbox Isolation)
* **رابط امن پرتاب ویندوز (ISecureLauncher):** جایگزینی کامل کدهای ناامن مستقیم `Process.Start` با ساختار مدرن Win32 `CreateProcessAsUser` با استفاده از توکن کاربر فعال شبیه‌سازی شده و محیط جداگانه routed به ایستگاه کاری ویندوز (`winsta0\default`).
* **مدیریت منابع با Job Objects:** تخصیص منابع سخت‌افزاری شامل محدودسازی حافظه رم فیزیکی، همبستگی پردازنده (CPU Affinity)، اولویت اجرای پروسه و ویژگی حیاتی **Kill-On-Close** جهت نابود کردن خودکار تمامی فرآیندهای فرزند بازی در صورت بسته شدن یا کراش کلاینت.
* **ایزولاسیون دایرکتوری و ریجستری (Sandbox Directory & Registry Isolation):**
  - ساخت خودکار دایرکتوری‌های کاملاً مجزای شبیه‌سازی شده برای فایل‌های ذخیره بازی (`SaveData`)، دایرکتوری موقت (`Temp`) و کش (`Cache`) با دسترسی‌های محدود و پاکسازی تضمینی در صورت پایان یافتن سشن یا کراش سیستم.
  - مجازی‌سازی خودکار کلیدهای رجیستری ویندوز برای بازی‌ها تحت مسیر ایزوله شده `HKCU\Software\SAYRA_Virtual` جهت جلوگیری از تداخل سشن‌های موازی.
* **مسدودسازی USB و درایوهای خارجی:** ارزیابی سیاست‌های امنیتی و خروج اجباری (Eject/Dismount) امن فلش مموری‌ها و رسانه‌های ذخیره‌سازی نامعتبر حین اجرای بازی‌ها.
* **پوشش بی‌نقص گرافیکی و تداخل صفر (Click-Through Overlay):** طراحی یک لایه Overlay پیشرفته WPF با استایل‌های بومی ویندوز (`WS_EX_TRANSPARENT` و `WS_EX_NOACTIVATE`) که با تداخل صفر در زوایه بالای سمت راست مانیتور اصلی قرار گرفته، کلیک ماوس و فشرده شدن کلیدهای کیبورد را بدون سرقت فوکوس از بازی عبور می‌دهد.

### ۳. پلتفرم پویای بروزرسانی سازمانی (Enterprise Update Platform)
* **بروزرسانی تراکنشی اتمیک:** استفاده از ماژول `AtomicFileReplacer` برای جایگزینی فایل‌های باینری به صورت اتمیک و لایه‌های بازگشت به عقب خودکار در صورت بروز خطا.
* **فرمت توزیع امن پکیج ها (.spk):** پیاده‌سازی خواننده و نویسنده استریم پکیج‌های انحصاری `.spk` با اعتبارسنجی دیجیتال از طریق امضای ECDsa-P384 و کلیدهای عمومی RSA.
* **زمان‌بندی هوشمند و مانیتورینگ پهنای باند (Bandwidth Limit):**
  - مجهز به الگوریتم سطل توکن (Token Bucket) جهت مدیریت دقیق سرعت دانلود به صورت مگابایت بر ثانیه.
  - هماهنگ با مناطق زمانی و پنجره‌های نگهداری (Maintenance Windows) برای مسدود کردن نصب آپدیت در ساعات شلوغ گیم‌نت.
  - هماهنگی با SCM و اجرای امن بدون نیاز به دسترسی مستقیم کاربر با بکارگیری `PrivilegeManager` و نصب در لایه سیستم.

### ۴. موتور خودترمیمی، سلامت و انعطاف‌پذیری (Resilience, Health & Self-Healing)
* **پایش سلامت زیرسیستم‌ها (Health Monitor):** پایش مداوم تمام ماژول‌های فعال سیستم و محاسبه پویای نمره سلامت سیستم با فرمول‌های ریاضی پیچیده و توزیع رویدادها به لایه‌های بالاتر.
* **صف اولویت‌دار خودترمیمی (RecoveryQueue):** بازسازی و رفع اشکال خودکار سیستم‌ها با ۱۵ استراتژی مجزا (از جمله بازسازی پایگاه داده رمزگذاری شده، راه‌اندازی مجدد سرویس‌های شبکه، بازیابی صف‌های دانلودی قطع شده و غیره) بدون نیاز به ری‌استارت دستی سیستم.
* **سیستم شناسایی حلقه خرابکاری (Loop Detector):** جلوگیری از تکرار بیهوده تلاش‌های ترمیمی که باعث بار اضافی روی سخت‌افزار می‌شوند به همراه قرنطینه کردن ماژول‌های ناپایدار.

### ۵. پلتفرم پایش، ردیابی و تلمتری (Observability, Telemetry & Tracing)
* **گردآورنده‌های سخت‌افزاری و نرم‌افزاری:** اجرای همزمان ۱۶ گردآورنده (شامل منابع اصلی سیستم، اتصالات IPC، وضعیت فرآیندها، پلاگین‌ها، ماژول‌های همگام‌سازی، دانلودرها و ده‌ها شاخص پیشرفته دیگر).
* **کاهش حجم هوشمند داده‌ها (Downsampling):** تجمیع هوشمند داده‌های زمانی تلمتری با مدل‌های ریاضی پیشرفته مانند میانگین متحرک نمایی (EMA)، انحراف معیار، و محاسبه صدک‌های با دقت بالا (P50, P90, P95, P99).
* **ردیابی توزیع شده (Distributed Tracing):** ردیابی جریان فرآیندها به صورت توزیع شده با ارسال خودکار هدرهای `TraceId` و `CorrelationId` از لایه کلاینت به وب و در طول لوله‌های IPC Named Pipe با استفاده از لایه غیرمسدودکننده `AsyncLocal`.
* **موتور هشدار هوشمند (Alert Engine):** ارزیابی موازی و بلادرنگ قوانین هشداردهی (CPU، دیسک، امنیت، شبکه و غیره) با استفاده از فیلترهای ضد تکرار (Fingerprint Deduplication)، لایه‌های سرکوب هشدار و ارتقای اولویت.

### ۶. موتور مدیریت ناوگان و عملیات راه دور (Enterprise Fleet Management & Remote Ops)
* **پروتکل فرمان‌های راه دور (Remote Commands Engine):** پشتیبانی از ۲۰ دستور ساختاریافته امن و رمزنگاری‌شده (شامل قفل/باز کردن سیستم، راه‌اندازی ابزارهای کمکی، اجرای اسکریپت‌ها، و مدیریت سرویس‌ها) که از فیلترهای احراز هویت، جلوگیری از Replay و ۸ میان‌افزار (Middleware) عبور می‌کنند.
* **انتقال فایل امن و پرسرعت (Remote File Management):**
  - انتقال تکه‌ای (Chunk-based) و موازی فایل‌ها به صورت کاملاً ایمن با قابلیت توقف و رزومه خودکار.
  - اعتبارسنجی آدرس‌ها جهت بلاک کردن کامل حملات پیمایش مسیر (`../` Path Traversal) و دسترسی‌های غیرمجاز به پوشه‌های سیستمی ویندوز.
* **مدیریت دارایی‌ها و وظایف نگهداری (Asset & Maintenance Engine):** جمع‌آوری لحظه‌ای دارایی‌های سخت‌افزاری و نرم‌افزاری و اجرای وظایف خودکار نگهداری سیستم به همراه موتور ماشین حالت هوشمند.

---

## Technical Overview of Implemented Modules (English)

### 1. Secure IPC & Crypto
* **SQLCipher Encrypted Local Storage:** All local databases (offline queues, audit logs, metrics, assets) are encrypted using SQLCipher with individual 256-bit AES master keys enveloped securely via Windows DPAPI.
* **Hardened Named Pipe IPC:** Restrictive DACLs configured to allow access exclusively to the `SYSTEM` account, Local Administrators, and the active console user (`InteractiveSid`), effectively thwarting IPC spoofing and tampering.
* **Anti-Replay Handshake:** Time-skew checks and transient nonces block any message-replay attempts.

### 2. Secure Game Launch Pipeline & Sandbox Isolation
* **Enterprise Secure Launcher (`ISecureLauncher`):** Complete replacement of vulnerable direct `Process.Start` logic with Windows native `CreateProcessAsUser` targeting the active user session token routed directly to `winsta0\default`.
* **Resource Limitation via Job Objects:** Strict memory limits, CPU affinity masks, and process priorities enforced globally. Includes the mandatory **Kill-On-Close** feature which ensures clean process tree cleanup of game executables on client termination.
* **Sandbox Directory & Registry Isolation:**
  - Automated sandbox directories for game saves (`SaveData`), temporary files (`Temp`), and cache (`Cache`) created dynamically and scrubbed thoroughly upon session termination.
  - Registry virtualization mapping game-specific keys under isolated `HKCU\Software\SAYRA_Virtual\{SessionId}\{GameId}` paths.
* **USB & Removable Media Blocker:** Automatically unmounts and ejects non-compliant removable storage devices before and during gameplay.
* **Click-Through WPF Overlay:** A topmost, borderless overlay utilizing native styles `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` positioned seamlessly at the top-right of the primary monitor, allowing total click-through and keyboard pass-through.

### 3. Enterprise Update Platform
* **Atomic Deployment:** Standardized transactional binary swaps with automatic fallback rollback via `AtomicFileReplacer` if staging or checksum validation fails.
* **Secure `.spk` Package Format:** Secure block-by-block package streaming authenticated using ECDsa-P384 signatures and RSA public key verification.
* **Throttled Download Engine:** Real-time Token Bucket bandwidth limiter, timezone-aware maintenance windows, and SHA-256 validation mechanisms. Fully decoupled from CI via specialized OS wrappers.

### 4. Resilience, Health & Self-Healing
* **Subsystem Health Scoring:** Highly reliable health scoring and real-time heartbeat monitoring.
* **Self-Healing Orchestrator:** Priority-based recovery pipeline backed by 15 dedicated strategies for instant local state repair.
* **Loop Prevention Quarantine:** Detects recurring failures and automatically quarantines unstable subsystems.

### 5. Observability, Telemetry & Tracing
* **Dual-Mode Telemetry Gathering:** Real-time physical and logical metric tracking using non-blocking high-performance `.NET` channels.
* **Statistical Metrics Aggregation:** High-precision percentiles (P50, P90, P95, P99), variance, and downsampling.
* **Ambient Distributed Tracing:** Seamless trace propagation across thread boundaries and IPC channels utilizing safe `AsyncLocal` contexts.
* **Advanced Alert Engine:** Highly scalable Alert manager featuring fingerprint deduplication, priority escalation, and multi-layered suppression.

### 6. Enterprise Fleet Management & Remote Ops
* **Secure Remote Commands:** Execution of 20 distinct system commands via a high-security dispatcher pipeline featuring 8 dedicated middlewares.
* **Secure Path-Validated File Transfer:** Stream-based chunk transfers utilizing Token Bucket throttling and strict directory validation to prevent Path Traversal and unauthorized folder access.
* **Assets & Maintenance Scheduler:** Hardware and software inventory collectors integrated with persistent database schemas and a stateful task execution machine.

---

## نحوه راه‌اندازی و توسعه (Installation & Development)

### پیش‌نیازها / Prerequisites
* **.NET SDK 8.0**
* **Windows 10/11** (برای اجرای بومی سرویس‌های ویندوز) یا هر پلتفرمی جهت کامپایل و اجرای تست‌ها.

### ساخت پروژه / Build
برای کامپایل کل مجموعه در حالت ریلیز از دستور زیر استفاده کنید:
```bash
dotnet build Sayra.Client.sln -c Release
```

### اجرای تست‌های جامع / Running Tests
کل پروژه دارای بیش از **۵۰۰ تست واحد (Unit Test) و تست ادغام (Integration Test)** بسیار سخت‌گیرانه است که پوشش ۱۰۰ درصدی امنیت و منطق تجاری را تضمین می‌کند. برای اجرای تست‌ها:
```bash
dotnet test Sayra.Client.Configuration.Tests/Sayra.Client.Configuration.Tests.csproj --filter "FullyQualifiedName~Sayra.Client.Configuration.Tests.Phase9"
```

---

## امنیت و پایبندی به استانداردها (Security & Compliance)

کلاینت سایرا با بالاترین استانداردهای امنیتی توسعه یافته و در آزمون‌های نفوذ زیر سربلند بوده است:
* **حفاظت در برابر حملات تزریق کد (Code Injection Prevention):** به صفر رساندن امکان اجرای هرگونه فایل ناامن خارج از فضای کاری سندباکس شده.
* **جلوگیری از پیمایش فایل غیرمجاز (Path Traversal Protection):** تمامی مسیرهای دانلود و انتقال فایل تحت دایرکتوری مجاز سیستم به شدت ارزیابی شده و مسیرهای مشکوک مانند `../` در همان لایه فیلتر، مسدود و گزارش می‌شوند.
* **دفاع چندلایه رمزنگاری:** غیرممکن بودن سرقت اطلاعات ذخیره شده محلی به علت استفاده از پیوند فیزیکی DPAPI با سیستم‌عامل کاربر بر روی فایل رمزگذاری شده توسط موتور SQLCipher.

---

### Meta: Documentation Updates

* **Current Status:** 100% Certified, Production Hardened, and Verified across Windows Host and Linux CI platforms.
* **Language Support:** Native Persian Right-to-Left (RTL) styling ready, fully localized text components, with comprehensive dual-language documentation.
