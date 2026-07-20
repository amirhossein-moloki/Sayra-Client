# BankYar Settings Screen UX/UI Design Specification
**Application Type:** Offline-first Financial SMS Manager
**Platform:** Android (Future iOS)
**Framework:** Flutter (Design Only, Production-Ready Layout/Tokens)
**Design Language:** Material Design 3 (MD3)
**Primary Language:** Persian / فارسی (RTL-first)
**Theme:** Professional, Minimal, Secure, Privacy-first

---

## 1. Complete Screen Layout & Navigation Architecture

BankYar's Settings UI is structured to maximize visual clarity, minimize cognitive load, and align with Material Design 3 specifications. Because this is a high-security, offline-first financial application, layout choices emphasize immediate control and absolute privacy transparency.

```
+-------------------------------------------------------------------+
| [RTL Top App Bar]                                                 |
|  (User Profile Avatar)            تنظیمات (Settings)  [Search Icon] |
+-------------------------------------------------------------------+
|  [Horizontal Progress Bar - Security Audit Score: 92% (Green)]    |
+-------------------------------------------------------------------+
|                                                                   |
|  بخش کاربری (USER PROFILE) - آینده                                  |
|  +-------------------------------------------------------------+  |
|  | [Icon] حساب کاربری (تنظیم نشده - حالت آفلاین فعال است)         |  |
|  +-------------------------------------------------------------+  |
|                                                                   |
|  امنیت و حریم خصوصی (SECURITY & PRIVACY)                          |
|  +-------------------------------------------------------------+  |
|  | [Icon] قفل با کد PIN                     [Toggle Switch: On] |  |
|  | [Icon] تایید هویت زیست‌سنجی (بیومتریک)   [Toggle Switch: On] |  |
|  | [Icon] مدت زمان قفل خودکار               [متوسط - ۵ دقیقه >] |  |
|  | [Icon] وضعیت رمزنگاری داده‌ها                  [فعال و ایمن] |  |
|  | [Icon] ممیزی امنیتی                           [۹۲٪ - عالی >] |  |
|  | [Icon] وضعیت دستگاه‌های قابل اعتماد         [دستگاه فعلی فعال] |  |
|  | [Icon] حالت کاملاً آفلاین (Offline Mode)  [همیشه فعال - قفل] |  |
|  | [Icon] خلاصه داده‌های جمع‌آوری شده           [بدون اشتراک‌گذاری >] |  |
|  | [Icon] دسترسی به پیامک‌ها (SMS Access)           [تایید شده >] |  |
|  | [Icon] وضعیت تحلیل‌های آماری (Analytics) [Toggle Switch: Off] |  |
|  | [Icon] گزارش خطا و کرش (Crash Reporting) [Toggle Switch: Off] |  |
|  | [Icon] سیاست نگهداری داده‌ها             [۳۰ روز - خودکار >] |  |
|  +-------------------------------------------------------------+  |
|                                                                   |
|  مدیریت مالی و تراکنش‌ها (BANKING & TRANSACTIONS)                    |
|  +-------------------------------------------------------------+  |
|  | [Icon] مدیریت بانک‌ها و حساب‌ها                      [۳ بانک فعال >] |  |
|  | [Icon] ترجیحات تراکنش‌ها (محدودیت‌ها، دسته‌بندی‌ها)                |  |
|  +-------------------------------------------------------------+  |
|                                                                   |
|  پشتیبان‌گیری و انتقال داده (BACKUP & EXPORT)                         |
|  +-------------------------------------------------------------+  |
|  | [Icon] پشتیبان‌گیری و بازیابی داده‌ها                  [دستی >] |  |
|  | [Icon] خروجی / ورودی فایل (Excel, JSON)              [فشرده >] |  |
|  +-------------------------------------------------------------+  |
|                                                                   |
|  تنظیمات عمومی و ظاهری (PREFERENCES & APPEARANCE)                  |
|  +-------------------------------------------------------------+  |
|  | [Icon] اعلان‌ها و هشدارها (صدا، لرزش، ساعات سکوت)               |  |
|  | [Icon] پوسته و تم (تم سیستمی، حالت تاریک)             [روشن >] |  |
|  | [Icon] زبان برنامه (Language)                         [فارسی >] |  |
|  | [Icon] دسترسی‌پذیری (کنتراست بالا، متن بزرگ، کاهش انیمیشن) >    |  |
|  | [Icon] مجوزهای دسترسی سیستم (System Permissions)      [مدیریت >] |  |
|  | [Icon] فضای ذخیره‌سازی و حافظه موقت (بهینه‌سازی دیتابیس) >       |  |
|  +-------------------------------------------------------------+  |
|                                                                   |
|  درباره برنامه و پشتیبانی (ABOUT & SUPPORT)                         |
|  +-------------------------------------------------------------+  |
|  | [Icon] پشتیبانی و ارتباط با ما                     [تیکت آفلاین >] |  |
|  | [Icon] قوانین، حریم خصوصی و مجوزها (کدهای متن‌باز)               |  |
|  | [Icon] اطلاعات نسخه و ساخت                      [نسخه ۱.۴.۰ >] |  |
|  +-------------------------------------------------------------+  |
+-------------------------------------------------------------------+
```

### Visual Grid & Layout Rules
- **Viewport Constraints:** Designed for a standard Android aspect ratio (typically 20:9, e.g., 1080x2400 px), fully scaling down to compact screens (360dp width) and adapting to tablet viewports via a split-pane layout (Settings Categories list on the left, Active category details on the right).
- **RTL Alignment:** All layouts are strictly mirrored for Farsi. Leading elements (Icons) align to the right margin, text flows from right to left, and trailing elements (switches, arrows, status text badges) are placed on the far left.
- **Top App Bar:** Medium Top App Bar specification from MD3. Scroll behavior is **Collapsing** (shifts into a compact 56dp Top App Bar on scroll).
- **Scrolling Model:** Single-page scroll with sticky grouping headers to maintain context. Fast-scroll thumb is enabled.

---

## 2. Settings Hierarchy & Navigation Map

```
Settings Screen (Main Hub)
├── [Profile Section] (Future Extension - Currently Disabled / Placeholder)
├── [Security Section] (High Priority Group)
│   ├── PIN Lock (Toggle & PIN Entry Flow)
│   ├── Biometric Authentication (Toggle / Hardware Check)
│   ├── Auto Lock Timeout (Dropdown / Selector Dialog)
│   ├── Encryption Status (Information View - Cryptographic Standard Indicator)
│   ├── Security Audit (Visual Score Gauge & Recommendations)
│   └── Trusted Device Status (Hardware Identity & Signature Verifier)
├── [Privacy Section] (High Priority Group - Offline-First Focus)
│   ├── Offline Mode Status (Always-On Immutable Toggle)
│   ├── Collected Data Summary (Interactive Sandboxed Data Graph)
│   ├── SMS Access Status (Android Permission Bridging Sheet)
│   ├── Analytics Status (Opt-in Switch - Default Off)
│   ├── Crash Reporting Status (Opt-in Switch - Default Off)
│   └── Data Retention Policy (Scheduler Selector)
├── [Bank Management] (Custom Finance Category)
│   ├── Active SMS Triggers (Regex mappings per Bank Card)
│   └── Manual Bank Profiles (Color Branding & Custom Bank Cards)
├── [Transaction Preferences]
│   └── Categories Mapping & Smart SMS Parsers
├── [Backup & Restore]
│   ├── Create Local Backup (Encrypted Archive .bybak)
│   └── Restore Backup (Decryption & Hash Check Flow)
├── [Import / Export]
│   ├── Export to Encrypted Excel/JSON
│   └── Import CSV Bank Statements
├── [Notifications]
│   ├── Enable Notifications (Master Switch)
│   ├── Sound (System Ringtone Picker integration)
│   ├── Vibration (Pattern Selector)
│   ├── Priority (Urgent / Default / Silent)
│   ├── Notification Actions (Interactive Action buttons in tray)
│   └── Quiet Hours (Time Range Scheduler)
├── [Appearance]
│   ├── Theme Configuration (Dynamic Color / Custom Seed Color)
│   ├── Dark Mode Settings (On / Off / System Adaptive)
│   ├── Font Size Scaler (Fluid Dynamic Slider)
│   ├── Display Density Selector (Compact / Comfortable / Spacious)
│   └── System Animations (Toggle Switch for Performance Boost)
├── [Language]
│   └── System Default (Persian RTL primary, English LTR secondary)
├── [Accessibility]
│   ├── High Contrast Toggle
│   ├── Large Text Mode
│   ├── Reduce Motion (Global Animation Dispatcher Disable)
│   ├── Screen Reader Optimizations (Semantic Label Overlay)
│   └── RTL Live Preview
├── [Permissions]
│   └── App-level OS Permission Dashboard
├── [Storage]
│   ├── Database Size Analyzer
│   ├── Backup Archive Manager
│   ├── Cache Size visualizer
│   ├── Clear Cache Action (Safe Purge)
│   └── Optimize & Re-index Database (SQLite VACUUM command trigger)
├── [Developer Options] (Double-Tap Build Number Trigger - Hidden by Default)
│   ├── Database Inspector
│   ├── Simulated SMS Receivers
│   └── Decryption Key Rotators
└── [About Application]
    ├── Version Info & Build Number
    ├── Offline Support & Local Contact Center
    ├── Privacy Policy & Off-grid Data Manifest
    ├── Terms of Service
    └── Open Source Licenses & Attribute list
```

---

## 3. Comprehensive Component Specifications

To achieve an enterprise-grade experience, we define the complete visual, interactive, accessibility, and RTL parameters for **every single setting element**.

---

### GROUP A: SECURITY COMPONENTS

#### 1. PIN Lock Configuration
- **Purpose:** Restricts unauthorized app entry using a local secure 4-to-6-digit personal identification number.
- **Business Value:** Prevents secondary access to sensitive financial SMS records if the Android device is unlocked and handed to another user.
- **Visual Priority:** Critical (Priority 1).
- **Placement:** Topmost position in the "Security" sub-section.
- **Spacing:** Height: 56dp, Vertical Padding: 8dp, Horizontal Padding: 16dp.
- **Icons:** Leading: `MdIcons.lock_outline` (Size: 24dp, Color: `sys.color.onSurfaceVariant`). Trailing: `MdIcons.switch` or Arrow to PIN Setup.
- **Typography:** Title: `titleMedium` (Vazirmatn Medium, 16sp, Color: `sys.color.onSurface`), Description: `bodySmall` (Vazirmatn Regular, 12sp, Color: `sys.color.onSurfaceVariant`).
- **States:**
  - *Default:* White/Dark Gray background matching surface.
  - *Hover/Focus:* Subtle tint matching `sys.color.surfaceVariant`.
  - *Active/Pressed:* `sys.color.primaryContainer` (12% opacity overlay).
  - *Loading:* Pulse animation on trailing switch.
  - *Disabled:* Opacity 38%, non-interactive.
  - *Error:* Red theme tint (`sys.color.error`) if PIN storage hardware is corrupted.
- **Accessibility:** Semantic Label: "رمز عبور عددی برای ورود به برنامه". High contrast target ratio: 4.5:1.
- **RTL Behaviour:** Mirror layout horizontally. Switch aligns to far-left. Label and Lock icon align to far-right.
- **Animation:** Toggle switch transitions using a smooth Material 3 slide-and-fill animation (duration: 200ms).
- **Future Expansion:** Support for alphanumeric PIN patterns and security pattern drawings.

#### 2. Biometric Authentication
- **Purpose:** Integrates with Android BiometricPrompt (Fingerprint/Face Unlock).
- **Business Value:** High-speed, secure, and frictionless authentication without manual entry.
- **Visual Priority:** High (Priority 1).
- **Placement:** Immediately below PIN Lock.
- **Spacing:** Height: 56dp, Vertical Padding: 8dp, Horizontal Padding: 16dp.
- **Icons:** Leading: `MdIcons.fingerprint` (Size: 24dp, Color: `sys.color.onSurfaceVariant`). Trailing: Switch.
- **Typography:** Title: `titleMedium` (16sp), Description: `bodySmall` (12sp).
- **States:** Same standard state tokens. If device hardware lacks biometric sensor, state is **Disabled** automatically, and description displays "این دستگاه فاقد حسگر زیست‌سنجی است".
- **Accessibility:** Label: "تایید هویت با اثر انگشت یا چهره".
- **RTL Behaviour:** Switch aligns left, icon and text right.
- **Animation:** standard Switch slide animation.
- **Future Expansion:** Support for biometric fallback prioritization.

#### 3. Auto Lock Timeout
- **Purpose:** Specifies duration of inactivity before application auto-locks.
- **Business Value:** Protects data if user forgets to close the application.
- **Visual Priority:** Medium (Priority 2).
- **Placement:** Third item in Security list.
- **Spacing:** Height: 56dp, Padding: 8dp vertical, 16dp horizontal.
- **Icons:** Leading: `MdIcons.timer` (Size: 24dp). Trailing: Dynamic Status Indicator Text ("۵ دقیقه") with a chevron arrow (`MdIcons.chevron_left` for RTL).
- **Typography:** Title: `titleMedium` (16sp), Trailing value: `labelLarge` (Vazirmatn Bold, 14sp, Color: `sys.color.primary`).
- **States:**
  - *Default:* Transparent container.
  - *Active:* Navigation overlay.
- **Accessibility:** Announces "مدت زمان قفل خودکار؛ انتخاب شده روی ۵ دقیقه".
- **RTL Behaviour:** Chevron points left. Text on far-left, label on far-right.
- **Animation:** Ripple effect originates from touch center outward (duration: 150ms).
- **Future Expansion:** "Immediate Lock on App Minimization" option.

#### 4. Encryption Status
- **Purpose:** Displays structural cryptographic information regarding the SQLCipher/Room database.
- **Business Value:** Reassures users with visual proof of active AES-256 GCM encryption.
- **Visual Priority:** Medium-High (Priority 2).
- **Placement:** Fourth item in Security.
- **Spacing:** Height: 64dp (extra vertical space for detailed security text).
- **Icons:** Leading: `MdIcons.verified_user` (Size: 24dp, Color: `sys.color.primary`). Trailing: Safe Badge (Solid chip-style box with text: "ایمن - AES-256").
- **Typography:** Title: `titleMedium` (16sp), Subtitle: `bodySmall` (11sp, Green tint `sys.color.primary`).
- **States:** Read-only (Non-clickable, cannot be disabled).
- **Accessibility:** Announces: "وضعیت رمزنگاری دیتابیس فعال است و با استاندارد آ‌ئی‌اس ۲۵۶ محافظت می‌شود".
- **RTL Behaviour:** Badge moves left, verified icon right.
- **Animation:** None (static info element).
- **Future Expansion:** Rotate encryption keys directly from this element.

#### 5. Security Audit Indicator
- **Purpose:** Audits the current device and app settings for potential security vulnerabilities.
- **Business Value:** Drastically increases device security literacy, advising the user to lock their system bootloader, enable device screen locks, etc.
- **Visual Priority:** High (Priority 1).
- **Placement:** Fifth item in Security.
- **Spacing:** Height: 72dp. Uses a custom sub-progress bar underneath.
- **Icons:** Leading: `MdIcons.shield_search` (24dp). Trailing: Value badge "۹۲٪" + `MdIcons.chevron_left`.
- **Typography:** Title: `titleMedium` (16sp), Description: `bodySmall` (12sp).
- **States:** Clickable. Triggers deep-dive audit view.
- **Accessibility:** Reading order includes the current audit percentage.
- **RTL Behaviour:** Chevron is mirrored. Progress bar fills from right to left.
- **Animation:** Circular or linear green/yellow rating pulse.
- **Future Expansion:** Automated scheduling of background audits.

#### 6. Trusted Device Status
- **Purpose:** Verifies hardware bindings (Hardware-backed Keystore or Secure Enclave).
- **Business Value:** Guarantees that database cannot be cloned and decrypted on another phone model.
- **Visual Priority:** Medium (Priority 2).
- **Placement:** Bottom of Security Section.
- **Spacing:** Height: 56dp.
- **Icons:** Leading: `MdIcons.phonelink_lock` (24dp). Trailing: Small status indicator dot + Label "تایید سخت‌افزاری".
- **Typography:** Title: `titleMedium` (16sp), Value label: `bodySmall` (12sp, Color: green).
- **States:** Read-only with detailed dialog popup on tap.
- **Accessibility:** Announces: "سخت‌افزار دستگاه شما تایید شده و بومی است".
- **RTL Behaviour:** Label and status indicator dot align to left.
- **Animation:** None.
- **Future Expansion:** Option to export Hardware Signature key for physical backups.

---

### GROUP B: PRIVACY COMPONENTS

#### 1. Offline Mode Status
- **Purpose:** Shows and enforces absolute network-isolation.
- **Business Value:** Core architectural selling point. The app never sends data to any server, operating entirely in local space.
- **Visual Priority:** Critical (Priority 1).
- **Placement:** Topmost position in Privacy Section.
- **Spacing:** Height: 64dp.
- **Icons:** Leading: `MdIcons.wifi_off` (24dp, Color: `sys.color.tertiary`). Trailing: Lock icon and Badge "غیرقابل تغییر" (Immutable).
- **Typography:** Title: `titleMedium` (16sp), Subtitle: `bodySmall` (12sp, "داده‌های شما هرگز از این دستگاه خارج نمی‌شوند").
- **States:** Immutable. Always Active. Hover shows safety tooltip.
- **Accessibility:** Highly structured semantics stating network connection is physically blocked inside code.
- **RTL Behaviour:** Badge "غیرقابل تغییر" aligns to left, icon to right.
- **Animation:** Pulse wave on the wifi-off icon when entering Settings.
- **Future Expansion:** Toggleable local proxy routes for optional user-initiated manual backups.

#### 2. Collected Data Summary
- **Purpose:** Categorized display of size and contents of local tables (e.g., how many SMS parsed, transactions saved).
- **Business Value:** Maximum transparency over device storage contents.
- **Visual Priority:** High (Priority 1).
- **Placement:** Second in Privacy.
- **Spacing:** Height: 56dp.
- **Icons:** Leading: `MdIcons.analytics` (24dp). Trailing: `MdIcons.chevron_left`.
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall` (Count of stored transaction entries).
- **States:** Opens structured local visualizer chart.
- **Accessibility:** Speaks: "گزارش داده‌های محلی؛ شامل ۱۵۰۰ تراکنش ذخیره شده".
- **RTL Behaviour:** Elements mirrored correctly.
- **Animation:** Slide-in card transition.
- **Future Expansion:** Interactive charts showing monthly parsing counts.

#### 3. SMS Access Status
- **Purpose:** Controls system-level permission checking for receiving and reading SMS messages.
- **Business Value:** Core feature toggle.
- **Visual Priority:** Critical (Priority 1).
- **Placement:** Third in Privacy.
- **Spacing:** Height: 56dp.
- **Icons:** Leading: `MdIcons.sms` (24dp). Trailing: Permission Status Badge ("دسترسی مجاز است").
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall`.
- **States:** Tap redirects to Android Settings. If permission is revoked, turns Orange/Red with alert badge.
- **Accessibility:** Highlights urgent action if permission is missing.
- **RTL Behaviour:** Left-aligned badge, right-aligned text.
- **Animation:** Subtle blink on alert state.
- **Future Expansion:** Detailed breakdown of SMS read rates.

#### 4. Analytics Status
- **Purpose:** Consents to anonymized behavior patterns (Default: Opt-out / Disabled).
- **Business Value:** Aligns with GDPR, CCPA, and strict privacy principles.
- **Visual Priority:** Low (Priority 3).
- **Placement:** Fourth in Privacy.
- **Spacing:** Height: 56dp.
- **Icons:** Leading: `MdIcons.track_changes` (24dp). Trailing: Switch (Default: Disabled).
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall`.
- **States:** Interactive toggle.
- **Accessibility:** Clearly explains that turning this on is fully voluntary.
- **RTL Behaviour:** Mirrored layout.
- **Animation:** Switch slide transition.
- **Future Expansion:** Ability to view the raw JSON telemetry payload being sent if activated.

#### 5. Crash Reporting Status
- **Purpose:** Opt-in to local/anonymous crash dump exports for debugging (Default: Disabled).
- **Business Value:** Helps software stability without tracking identities.
- **Visual Priority:** Low (Priority 3).
- **Placement:** Fifth in Privacy.
- **Spacing:** Height: 56dp.
- **Icons:** Leading: `MdIcons.bug_report` (24dp). Trailing: Switch.
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall`.
- **States:** Standard switch states.
- **Accessibility:** Explains that report dumps redact financial figures.
- **RTL Behaviour:** Mirrored.
- **Animation:** standard Switch slide animation.
- **Future Expansion:** Local crash log viewer before upload.

#### 6. Data Retention Policy
- **Purpose:** Automatically purges records older than a specific date threshold.
- **Business Value:** Keeps DB lean, secure, and preserves disk space.
- **Visual Priority:** Medium (Priority 2).
- **Placement:** Bottom of Privacy section.
- **Spacing:** Height: 64dp.
- **Icons:** Leading: `MdIcons.auto_delete` (24dp, Color: `sys.color.error`). Trailing: Value ("۳۰ روز") + `MdIcons.chevron_left`.
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall` ("حذف خودکار پیامک‌های قدیمی").
- **States:** Opens selection sheet modal.
- **Accessibility:** "پاکسازی خودکار روی ۳۰ روز تنظیم شده است".
- **RTL Behaviour:** Mirrored layout.
- **Animation:** Slide up modal.
- **Future Expansion:** Option to choose custom archival retention filters per specific banks.

---

### GROUP C: GENERAL SETTINGS (NOTIFICATIONS, APPEARANCE, ACCESSIBILITY, STORAGE, BANKING)

#### 1. Notification Controller Group (Enable Notifications, Sound, Vibration, Priority, Quiet Hours)
- **Purpose:** Customizes native foreground notifications when an SMS is intercepted.
- **Business Value:** High engagement; instantly notifies user of budget overflows.
- **Visual Priority:** High (Priority 1).
- **Placement:** Under Preferences.
- **Spacing:** standard MD3 List tiles (56dp height each).
- **Icons:** Leading: `MdIcons.notifications` (24dp). Trailing: Toggle switches and dropdown spinners.
- **Typography:** Title: `titleMedium` (16sp), Subtitle: `bodySmall` (12sp).
- **States:** Complete group collapses or turns disabled (Opacity 38%) if "Master Notifications" switch is toggled OFF.
- **Accessibility:** Clear screen reader groups.
- **RTL Behaviour:** Mirrored toggle switch layout.
- **Animation:** Grid collapse/expand on group activation (duration: 300ms, Curves.easeInOut).

#### 2. Appearance & Theming Controller (Theme, Dark Mode, Font Size, Display Density, Animations)
- **Purpose:** Visual personalization, dynamic theme color selection, and dark mode toggles.
- **Business Value:** Modern MD3 feel, eases eye strain.
- **Visual Priority:** Medium (Priority 2).
- **Placement:** Under Preference group.
- **Spacing:** Height: 56dp-72dp. Font Size features a slider track.
- **Icons:** Leading: `MdIcons.palette` or `MdIcons.dark_mode`. Trailing: Standard Segmented Buttons or Custom Color Dot Trackers.
- **Typography:** Title: `titleMedium`, labels: `labelLarge`.
- **States:** Responsive sliders and selection blocks.
- **Accessibility:** Dynamically bound to system text scaler.
- **RTL Behaviour:** Sliders fill from right to left; dynamic RTL live preview updates all texts instantaneously.
- **Animation:** Dynamic color shifting fade effect (duration: 400ms).

#### 3. Accessibility Controls (High Contrast, Large Text, Reduce Motion, Screen Reader, RTL Preview)
- **Purpose:** Dedicated sub-settings for users with visual, auditory, or motor impairments.
- **Business Value:** Guarantees universal product design, widening active user-base.
- **Visual Priority:** High (Priority 1).
- **Placement:** Pre-configured section in main navigation hub.
- **Spacing:** Standard MD3 list layout.
- **Icons:** Leading: `MdIcons.accessibility` (24dp). Trailing: Switches.
- **Typography:** Title: `titleMedium`, Description: `bodySmall`.
- **States:** Real-time application update of contrast metrics.
- **Accessibility:** Self-descriptive accessibility triggers.
- **RTL Behaviour:** Perfect right-to-left layout alignment.
- **Animation:** Instantly stops all dynamic settings page transitions if "Reduce Motion" is turned ON.

#### 4. Storage & Performance (Database Size, Backup Size, Cache Size, Clear Cache, Optimize Database)
- **Purpose:** Local disk monitoring and optimization.
- **Business Value:** Extremely important for offline-first architecture to keep local databases highly performing over years of use.
- **Visual Priority:** High (Priority 1).
- **Placement:** Under general tools section.
- **Spacing:** Features custom 3-colored linear horizontal bar showing size usage relative to device partition.
- **Icons:** Leading: `MdIcons.storage` (24dp). Action: `MdIcons.cleaning_services` or `MdIcons.bolt` (Database Optimization).
- **Typography:** Title: `titleMedium`, Badge labels: `bodySmall`.
- **States:** Safe Destructive Actions. Tap on "بهینه‌سازی دیتابیس" runs SQLite VACUUM query with spinning progress indicator.
- **Accessibility:** Detailed auditory readout of memory consumption.
- **RTL Behaviour:** Storage usage bar starts filled from right to left.
- **Animation:** Linear bar fills progressively upon landing on page.

#### 5. Bank Management & Transaction Preferences
- **Purpose:** Configures custom SMS parsing expressions and maps specific card prefixes to custom banking profiles.
- **Business Value:** Core financial logic engine configuration without changing app codebase.
- **Visual Priority:** Critical (Priority 1).
- **Placement:** Middle of main Settings page (Financial Hub).
- **Spacing:** Card List, height: 72dp per active bank.
- **Icons:** Leading: Custom brand assets or `MdIcons.account_balance`. Trailing: Drag handles or edit buttons.
- **Typography:** Title: `titleMedium` (16sp), Subtitle: `bodySmall` (12sp).
- **States:** Displays empty state with custom illustration if no banks are linked yet.
- **Accessibility:** Direct touch zones (min 48x48 dp).
- **RTL Behaviour:** Mirrored perfectly.
- **Animation:** Slide and reorder animation for custom lists.

#### 6. Backup, Restore, Import & Export
- **Purpose:** Off-grid disaster recovery and data exchange.
- **Business Value:** Secure physical possession of financial data.
- **Visual Priority:** Critical (Priority 1).
- **Placement:** Security and data management section.
- **Spacing:** Spacing blocks of 16dp.
- **Icons:** Leading: `MdIcons.backup` or `MdIcons.file_download`. Trailing: Interactive progress ring or confirmation check.
- **Typography:** Title: `titleMedium`, Label: `bodySmall`.
- **States:** Interactive buttons with disabled behavior if export process is ongoing.
- **Accessibility:** Explains warnings carefully prior to wipe.
- **RTL Behaviour:** Mirrored layout.
- **Animation:** Rotating refresh icon for running operations.

---

### GROUP D: SUPPORT, INFO & HIDDEN DEVELOPER OPTIONS

#### 1. About Application (Version Info, Build Number, Support, Licenses)
- **Purpose:** Licensing metadata, build information, and local contact links.
- **Business Value:** Regulatory legal compliance and open-source transparency.
- **Visual Priority:** Low (Priority 3).
- **Placement:** Bottommost section of settings screen.
- **Spacing:** standard MD3 list.
- **Icons:** Leading: `MdIcons.info` (24dp). Trailing: `MdIcons.chevron_left`.
- **Typography:** Title: `titleMedium`, Subtitle: `bodySmall` (Vazirmatn Light, 11sp).
- **States:** Standard tap interaction. Double-tapping "Build Number" 7 times initiates the Developer Mode unlock.
- **Accessibility:** Standard screen reader support.
- **RTL Behaviour:** Perfect RTL mirror.
- **Animation:** Sparkle indicator on Developer Mode unlock.

#### 2. Developer Options (Hidden by Default)
- **Purpose:** Advanced database inspection, local debugging tools, and simulation logs.
- **Business Value:** Extremely fast field-testing and bug analysis.
- **Visual Priority:** Very Low (Only appears once unlocked).
- **Placement:** Appears dynamically at the very bottom of Settings.
- **Spacing:** Height: 56dp per row.
- **Icons:** Leading: `MdIcons.developer_mode` (24dp, Purple color). Trailing: Toggle.
- **Typography:** Title: `titleMedium` (System Monospace font for developer subtext).
- **States:** Dynamic section visibility.
- **Accessibility:** Accessible via standard semantic markers.
- **RTL Behaviour:** Layout mirrored correctly.
- **Animation:** Slide-down entry animation once activated.

---

## 4. Interaction Flow & State Machine

BankYar's Settings Screen relies on an explicit, state-driven interaction model. The layout remains responsive and predictable across all hardware environments.

```
                  [User Enters Settings Screen]
                               │
                      (Check State Flags)
                               │
           ┌───────────────────┴───────────────────┐
           ▼                                       ▼
  [Developer Mode: False]                [Developer Mode: True]
  (Developer Row is Hidden)              (Developer Row is Visible)
           │                                       │
           └───────────────────┬───────────────────┘
                               ▼
                    [Interaction Event]
                               │
       ┌───────────────────────┼───────────────────────┐
       ▼                       ▼                       ▼
  [Toggle Switch]        [Modal Trigger]       [Destructive Action]
  - Visual slide         - Darkened overlay    - Dialog Box popup
  - Haptic feedback      - Slide-up (300ms)    - Dual confirmation
  - Persistent state     - Bottom Sheet /      - Block interaction
    saved to disk          Custom Dialog         until resolved
```

### Transition Timing and Motion Easing
- **Page Transitions:** Under-page slider transition (Material 3 standard Shared Axis, 300ms duration).
- **Inter-Item Ripples:** `Curves.fastOutSlowIn` standard ripple expansion.
- **Expansion tiles:** Height animation with custom clip-rect boundaries to prevent layout flickering.

---

## 5. Dialog & Bottom Sheet Specifications

Due to the offline-first financial nature of BankYar, settings changes must occur in controlled, zero-error environments. We define three specific structural components here.

### A. Auto Lock Timeout Selection (Bottom Sheet)
Used to let users specify the duration before the app blocks screens.

```
+---------------------------------------------------------+
|                [Handle - Bottom Sheet Drag]             |
|                                                         |
|                 مدت زمان قفل خودکار برنامه              |
|        (Auto Lock Timeout - Vazirmatn Bold 18sp)        |
|                                                         |
|  انتخاب کنید بعد از چه مدت عدم فعالیت، برنامه قفل شود.   |
|                                                         |
|  ( ) بلافاصله (Immediate)                               |
|  ( ) ۱ دقیقه بعد (1 Minute)                             |
|  (•) ۵ دقیقه بعد (5 Minutes - Active selection)         |
|  ( ) ۱۵ دقیقه بعد (15 Minutes)                          |
|  ( ) ۳۰ دقیقه بعد (30 Minutes)                          |
|                                                         |
|  [دکمه تایید - Primary Button]   [انصراف - Text Button] |
+---------------------------------------------------------+
```
- **Dimensions:** Max Width: 400dp (centered on tablets), Height: Adaptive (approx. 380dp).
- **Corner Radius:** Top-Right & Top-Left: 28dp (Material 3 Shape Token).
- **Scrim Overlay:** Darkens background behind bottom sheet by 40% (`sys.color.scrim` with 0.4 opacity).
- **RTL Mirroring:** Radio buttons on the far-right, text flows RTL, Actions are on the bottom-left with primary action on the far-left.

---

### B. Database Optimization Progress (Custom Modal)
An active loading, blocking modal that locks screens during sensitive SQLite indexing.

```
+---------------------------------------------------------+
|                                                         |
|                 بهینه‌سازی پایگاه داده                  |
|          (Database Optimization - Bold 16sp)            |
|                                                         |
|           در حال مرتب‌سازی و کاهش حجم دیتابیس...         |
|                                                         |
|                        [ (O) ]                          |
|           (Circular Progress Indicator - Primary)        |
|                                                         |
|             لطفاً از برنامه خارج نشوید...               |
|                                                         |
+---------------------------------------------------------+
```
- **Backdrop:** Complete blocking overlay. Back-button is disabled during execution to prevent database corruption.
- **Dimensions:** Width: 280dp, Height: 200dp. Centered horizontally and vertically.
- **Animation:** Infinite rotation at 1.5 seconds per cycle.

---

## 6. Confirmation Patterns & Destructive Safeguards

Destructive operations (such as purging data or clearing cache) are locked behind specific confirmation layouts to avoid accidental loss.

### A. "Clear All Local Data" Dialog Specification

```
+---------------------------------------------------------+
| [Warning Icon - Red]  هشدار بسیار مهم (Critical Warning) |
+---------------------------------------------------------+
| آیا از حذف کامل تمام اطلاعات مالی و پیامک‌های ثبت شده در  |
| برنامه اطمینان دارید؟ این عملیات غیرقابل بازگشت است.    |
|                                                         |
| [ ] تایید می‌کنم که تمام داده‌های من برای همیشه پاک شود.   |
|     (Checkbox - Mandatory to enable Delete button)      |
|                                                         |
|     [حذف اطلاعات - Red Fill]   [انصراف - Outline Button] |
+---------------------------------------------------------+
```

#### Visual Behavior:
- **Delete Button State:** Default: **Disabled** (Opacity 38%, background gray). Action: **Enabled** ONLY when the user clicks the verification checkbox.
- **Haptic Feedback:** Triggers heavy haptic vibration patterns upon displaying this warning dialog.
- **Typography:** Warning text colored in `sys.color.error` with a high contrast index.

---

## 7. Accessibility Review & Universal Design Compliance

To ensure accessibility to visual and motor-impaired users, BankYar's Settings adheres strictly to the Web Content Accessibility Guidelines (WCAG 2.1 AA) translated into mobile environments:

| Requirement | Metric / Specification | Implementation Strategy |
|---|---|---|
| **Minimum Tap Targets** | Minimum 48 x 48 dp | Any clickable setting row is surrounded by an active padding region of 48dp height minimum, ensuring ease of thumb actions. |
| **Contrast Ratio** | WCAG 2.1 AA (4.5:1 ratio) | High-contrast visual themes with dedicated dark/light mode configurations. Status icons use colors matching accessible variants. |
| **Semantic Labels** | Comprehensive screen reader descriptions | Instead of reciting plain UI texts, labels declare functional statuses: "دکمه دوحالته فعال‌سازی اثر انگشت، هم‌اکنون روشن است." |
| **Reduced Motion Support** | Global flag checking | If system or in-app Reduce Motion is active, all layout transitions, sliding switches, and modal slides instantly transition to 0ms opacity cross-fades. |
| **High Contrast Mode** | Black/White/Yellow layout options | Uses custom high contrast seed themes, completely disabling thin gradients or light grey subtexts in favor of stark contrast patterns. |

---

## 8. Right-to-Left (RTL) Arabic & Persian Review

As Persian (فارسی) is the primary language, all layouts must be designed RTL-first to prevent broken layouts:

1. **Horizontal Layout Flow:** Mirroring is applied universally. Leading elements move to the right; trailing indicators move to the left.
2. **Font Families:** Vazirmatn is used as the default design font, using precise weight values (Light 300, Regular 400, Medium 500, Bold 700) supporting sub-pixel layout rendering.
3. **Punctuation Marks:** RTL punctuation orientation is maintained. Parentheses and colon marks wrap appropriately.
4. **Digit Display:** Standard Persian digits (۰، ۱، ۲، ۳، ۴، ۵، ۶، ۷، ۸، ۹) are used for values, and visual indicators.
5. **Chevron Arrows:** Chevron arrows indicating nested navigation point to the left (`<`) instead of the right (`>`).

---

## 9. Visual Consistency Checklist

Before converting the specifications into layout designs, use this checklist to ensure complete MD3 fidelity:

- [ ] All interactive elements use standard Material Design 3 state layer opacities (Hover: 8%, Focus: 10%, Pressed: 12%, Dragged: 16%, Disabled: 38%).
- [ ] Group titles use MD3 `titleSmall` token with an exact line height of 16dp and character spacing of 0.1px.
- [ ] Dividers are strictly 1dp thick, colored using the `sys.color.outlineVariant` token, with appropriate horizontal insets matching the text baseline (16dp).
- [ ] Every toggle switch is bounded by a visual tap target of 48x48dp, even if the switch container itself appears smaller.
- [ ] Color seeds strictly align to BankYar's professional palette (Primary: `#004F98` Secure Blue; Secondary: `#D4AF37` Gold Brand Tint; Neutral: `#F8F9FA` Light Gray / `#121212` Dark Minimal Gray).
- [ ] Standardized spacing scale is maintained (4dp, 8dp, 12dp, 16dp, 24dp, 32dp, 48dp) across all sub-components.

---

## 10. UX Validation Checklist

- [ ] Navigation depth from any configuration change to saving state is exactly zero (Auto-save pattern utilized across all settings).
- [ ] Critical destructive settings are protected by a confirmation dialog with a mandatory safety checkbox.
- [ ] Offline status indicator is permanently visible, confirming the app does not access external servers.
- [ ] Group sections do not exceed 5 items to avoid vertical scrolling fatigue.
- [ ] Search icon in the Medium Top App Bar offers quick navigation through all configuration properties.
- [ ] Build number is double-tapped 7 times to reveal Hidden Developer Options, preventing accidental developer-tool exposure.
