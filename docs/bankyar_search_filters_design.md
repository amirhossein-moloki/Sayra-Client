# BankYar (بانک‌یار) – Search & Advanced Filters Experience Specification
**Platform:** Android (Primary), iOS (Future) | **Framework:** Flutter (Target implementation) | **Design Language:** Material Design 3 (MD3) | **Layout Orientation:** Right-to-Left (RTL) | **Primary Language:** Persian (فارسی) | **Themes:** Professional, Minimal, Secure, Offline-First

---

## Executive Summary & Design Foundations

This specification details the production-ready visual design, information architecture, interaction flows, and system behavior for the **Search & Advanced Filters** screen of the **BankYar** mobile application. As a premium, secure, and offline-first personal finance and business transaction companion, the interface is optimized for high-performance retrieval over thousands of local records, while maintaining minimal cognitive load.

### Key Visual & Design Pillars (Material Design 3 Compliance)
1. **Adaptive Elevation & Surface Tonalities:** Following MD3 guidelines, elevation is represented using surface color overlays (`Surface Tint Color`) rather than traditional drop shadows, ensuring sleek and flat surfaces that scale beautifully across display types.
2. **Dynamic Color & Security Tokens:** The color palette relies on high-security and professional tones.
   - **Primary (Security Teal):** `#006A60` (Represents trust, security, and stability)
   - **On Primary:** `#FFFFFF`
   - **Secondary (Neutral Slate):** `#4A635F`
   - **Tertiary (Accent Gold/Amber):** `#705D00` (Used for key active highlights)
   - **Surface/Container Low:** `#F0FAF6` (Extremely light green/teal tint for background safety)
   - **Surface Container:** `#E9F4F0`
   - **Surface Container High:** `#DFEAE5`
   - **Outline:** `#707977`
   - **Error:** `#BA1A1A`
3. **Persian Typography (RTL Scale):** We utilize the **Vazirmatn** or **IRANSans** typeface. All line-heights and letter-spacing values are custom-tailored for Persian characters to avoid text truncation and descender-clipping.
4. **Touch & Ergonomic Targets:** All interactive components (chips, buttons, search input fields) strictly adhere to a minimum of **48 × 48 dp** touch target size with explicit margins.

---

## 1. Information Hierarchy & Complete Screen Layout

### Screen Layout Structure (Wireframe Representation - Visual Viewport)

Below is an ASCII blueprint of the screen container in RTL layout (`[Right] -> [Left]`).

```
========================================================================
[48dp] Top App Bar:
  [Voice Search* / Filter Active] [Active Query Clear] [Back Button/Arrow]
  * Voice Search is reserved for future expansion as a placeholder.
========================================================================
[56dp] Search Text Field:
  [Icon: Tune (Filter)] [Dynamic Inline Query Text] [Icon: Search (Magnifier)]
========================================================================
[40dp] Filter Chips Bar (Horizontal Scrollable, RTL):
  < [Favorites] >  < [Amount: Custom] >  < [Type: Income] >  < [Bank: All] >
========================================================================
[Viewport Content Area: Dynamically Switches Based on Screen State]

  STATE A: Idle (No search active)
  --------------------------------------------------------
  * Suggestion Chips:
    [Recent Transactions]  [This Month]  [High Amounts]
  * Recent Searches (Vertical List, RTL):
    [Icon: History]  "انتقال وجه ملت"               [Icon: Clear]
    [Icon: History]  "خرید سوپرمارکت"              [Icon: Clear]
    [Text Link: پاک کردن تاریخچه] (RTL Aligned Left)

  STATE B: Active Filtering / Search Results
  --------------------------------------------------------
  * Result Summary Bar:
    "۵ مورد یافت شد"                            [Icon: Save Filter*]
  * Transaction Result List (Vertical Scrollable):
    +--------------------------------------------------+
    | [Date Label: ۲۴ آذر ۱۴۰۲]                        |
    | [Item 1]                                         |
    |   [Favorite] [Amount: +2,500,000 ریال] [Bank Logo] |
    |   [Note Icon] [Details: واریز پایا از شرکت]      |
    +--------------------------------------------------+
    | [Item 2]                                         |
    |   [Normal]   [Amount: -450,000 ریال]   [Bank Logo] |
    |   [No Note]  [Details: خرید از فروشگاه افق کوروش] |
    +--------------------------------------------------+

  STATE C: Advanced Filter Panel Bottom-Sheet (Modal)
  --------------------------------------------------------
  [Swipe Handle]
  [Header: فیلترهای پیشرفته]                     [Button: ریست همه]
  --------------------------------------------------------
  - Keyword Match Input Field
  - Bank Selector (Horizontal Scrolling logos)
  - Transaction Type Segments (Income / Expense / Purchase / Transfer)
  - Amount Range Slider (Min to Max)
  - Date Range Picker (Quick Actions: Today, Week, Month, Custom Date)
  - Transaction Attributes (Has Note, Is Favorite)
  - Tag Collection (Multiple Choice Flow)
  --------------------------------------------------------
  [Button: اعمال فیلترها (Show X Results)] (Full-Width, Elevated MD3 Primary)
========================================================================
```

---

## 2. Comprehensive Component Specifications

### 2.1 Top App Bar
* **Purpose:** Standard navigation anchor and primary header context.
* **Visual Priority:** Level 1 (High).
* **Placement:** Positioned at the exact top of the viewport (`Y = 0`). Height: **64 dp**.
* **Spacing:** Right-side padding: `16 dp` (contains back arrow navigation in RTL). Left-side padding: `12 dp` (for system or search action options).
* **Typography:** Bold Vazirmatn Medium, `20 sp` (Line height: `28 dp`).
* **Icons:**
  - `arrow_forward` (RTL Back navigation) or `arrow_back` adjusted for RTL directionality.
  - `mic` (Voice Search placeholder - inactive/future release).
* **Elevation:** `0 dp` by default, transitions to `Level 1` surface overlay tint upon scrolling contents under it.
* **States:**
  - *Normal:* Neutral background (`#F0FAF6`).
  - *Offline:* Display banner at bottom of app bar showing "درحال کار به صورت آفلاین" in `#4A635F`.
* **Accessibility:** Accessible touch target of `48 × 48 dp` for the back button; semantic label: "بازگشت به صفحه قبل".
* **RTL Behaviour:** Inverted axis layout; the back arrow points to the right.
* **Animation:** Soft cross-fade transition when changing view context.
* **Future Expansion:** Add voice search speech-recognition triggers.

### 2.2 Search Field
* **Purpose:** Primary query input field utilizing live and debounced queries.
* **Visual Priority:** Level 1 (Highest interactive priority).
* **Placement:** Fixed below the App Bar. Height: **56 dp**. Full width with `16 dp` side margins.
* **Spacing:** Margins: `16 dp` Left/Right. Padding inside text input: `16 dp` Right (Start), `12 dp` Left (End).
* **Typography:** Vazirmatn Regular, `16 sp` (Line height: `24 dp`).
* **Icons:**
  - Lead icon (Right side in RTL): `search` (Magnifier search icon).
  - Trailing icon (Left side in RTL): `tune` (Filter adjustment icon) and `cancel`/`close` (Clear query).
* **Elevation:** Level 1 (`Surface Container` container color: `#E9F4F0`).
* **States:**
  - *Normal:* Outline color `#707977`, background `#E9F4F0`.
  - *Focused:* Outline shifts to Primary Theme Color `#006A60` with a `2 dp` stroke weight.
  - *Disabled:* Opacity reduced to `38%`, interaction disabled.
  - *Loading:* Trailing icon changes to a continuous 24dp circular progress indicator.
* **Accessibility:** Autofocus disabled on slow connections to prevent virtual keyboard popups during loading. Clear textual announcement on query updates.
* **RTL Behaviour:** Cursor flows right-to-left. Text alignment is strictly aligned right.
* **Animation:** Smooth horizontal expansion of the active text line marker.
* **Future Expansion:** Match history with dynamic keyword suggestions within the input dropdown.

### 2.3 Recent Searches
* **Purpose:** Quick access to historical search query terms to reduce repetitive typing.
* **Visual Priority:** Level 2 (Medium-Low).
* **Placement:** Content area, below Search Field when in idle/no-query state.
* **Spacing:** Top margin: `16 dp`. Item vertical padding: `12 dp`. Spacing between items: `8 dp`.
* **Typography:**
  - Header: Vazirmatn SemiBold, `14 sp` (`#4A635F`).
  - Item terms: Vazirmatn Regular, `14 sp` (`#191C1B`).
* **Icons:** `history` (leading icon in RTL, placed right), `close` (delete item icon, placed left).
* **Elevation:** Level 0 (Flat).
* **States:**
  - *Normal:* Slate-tint text color.
  - *Pressed:* High-contrast background overlay ripple (`#006A60` with 8% opacity).
  - *Empty State:* Title header is omitted if search history is completely cleared.
* **Accessibility:** "حذف" (Delete) button has a minimum tap target of `48 dp`.
* **RTL Behaviour:** Query text aligns right; delete history icon is positioned on the extreme left.
* **Animation:** Row item slides out to the left or fades out upon deletion.

### 2.4 Suggested Searches
* **Purpose:** Offer context-sensitive search topics (e.g., temporal or value-based shortcuts) to fast-track exploration.
* **Visual Priority:** Level 2.
* **Placement:** Horizontal-scrolling segment below the Search Field.
* **Spacing:** Horizontal padding of track: `16 dp`. Inter-chip spacing: `8 dp`.
* **Typography:** Vazirmatn Medium, `12 sp`.
* **Icons:** None or optional small contextual bank/brand icons.
* **Elevation:** Level 1 (`Surface Container High`).
* **States:** Highlighted with subtle accent color when corresponding to high-activity attributes.
* **RTL Behaviour:** Scroll direction moves from right to left natively.
* **Animation:** Staggered load animation when screen first initializes.

### 2.5 Filter Chips
* **Purpose:** Single-tap horizontal toggle buttons for immediate query refinement.
* **Visual Priority:** Level 1 (High operational priority).
* **Placement:** Immediately beneath the Search Input container. Height: **40 dp**.
* **Spacing:** Spacing between individual chips: `8 dp`. Scroll container padding: `16 dp` start/end.
* **Typography:** Vazirmatn Medium, `14 sp` (Line height: `20 dp`).
* **Icons:** Leading checkmark `check` icon when active; trailing dropdown arrow `arrow_drop_down` for filters requiring dropdowns (e.g., Bank select).
* **Elevation:** Level 1 (Default unselected), Level 2 (Active/Selected).
* **States:**
  - *Unselected:* Background `#F0FAF6`, Border `#707977`, Text `#4A635F`.
  - *Selected:* Background Primary Light Container (`#006A60` dynamic blend), Border omitted, Text `#FFFFFF`, Leading check icon visible.
  - *Hovered/Focused:* Light primary ripple overlay.
* **Accessibility:** Selected state clearly announced by screen readers as "فعال شد / Selected".
* **RTL Behaviour:** Left-to-right swipe path for natural Persian reading flow.
* **Animation:** Animated color transitions between state changes (duration: `200 ms`).

### 2.6 Advanced Filter Panel (Bottom-Sheet)
* **Purpose:** Deep query customization over extensive transactional fields.
* **Visual Priority:** Level 1 (Modal interaction container).
* **Placement:** Slides up from bottom of the viewport covering up to 85% of screen height.
* **Spacing:** Core internal padding: `24 dp` around fields. Bottom-sheet handle size: `32 × 4 dp`.
* **Typography:**
  - Modal Title: Vazirmatn Bold, `18 sp`.
  - Section Labels: Vazirmatn Medium, `14 sp`.
  - Content details: Vazirmatn Regular, `14 sp`.
* **Icons:** `tune`, `close`, `calendar_today`, `check_circle`.
* **Elevation:** Level 3 (Premium high surface shadow/tint to emphasize modal dominance).
* **States:**
  - *Normal:* Modal sheet fully interactive.
  - *Incomplete Inputs:* Form controls validation states with error borders on bad date ranges.
* **RTL Behaviour:** All switches, sliders, checkbox matrices align right-to-left.
* **Animation:** Slide-up transition (`300 ms` Cubic-Bezier curve) on open; slide-down on close/dismiss.
* **Future Expansion:** Custom criteria templates ("Saved Filters") to apply complex setups in one click.

### 2.7 Search Result Summary
* **Purpose:** Quantify active filter output and provide quick reset paths.
* **Visual Priority:** Level 3 (Informational).
* **Placement:** Above the transaction results vertical feed. Height: **36 dp**.
* **Spacing:** Margins: `16 dp` Left/Right. Padding: `4 dp` top/bottom.
* **Typography:** Vazirmatn SemiBold, `13 sp` (Color: `#4A635F`).
* **Icons:** `bookmark_border` (for saving search queries/filters in future updates).
* **Elevation:** Level 0 (Flat layout).
* **States:** Invisible when there are no query terms or zero active filters.
* **RTL Behaviour:** Counts are displayed on the right; "Save Filter" or "Clear All" acts on the left.

### 2.8 Transaction Result List
* **Purpose:** Core list view of transactions matching search parameters.
* **Visual Priority:** Level 1 (Highly read-dense area).
* **Placement:** Primary scrollable center viewport.
* **Spacing:** Individual transaction row height: **72 dp**. Inner horizontal padding: `16 dp`. Inter-row gap: `4 dp`.
* **Typography:**
  - Bank Name / Beneficiary Title: Vazirmatn SemiBold, `15 sp` (`#191C1B`).
  - Transaction Date & Time: Vazirmatn Regular, `12 sp` (`#707977`).
  - Amount: Vazirmatn Bold, `16 sp` (Color changes dynamically based on Type: Positive/Income: `#006A60`, Negative/Expense: `#BA1A1A`).
* **Icons:**
  - Bank Logos: Circular vectors, size `36 × 36 dp` with thin borders.
  - Inline Indicators: Small `attachment`/`note` icon (note indicator), `star` icon (favorite indicator).
* **Elevation:** Level 1 (Flat card styled via subtle `#E9F4F0` frame borders).
* **States:**
  - *Highlighted Matched Keywords:* The substring matching the user query is colored in Accent Yellow `#705D00` background or semi-bold primary color to visually pop out.
* **Accessibility:** Rich screen reader text: "تراکنش بانک ملی، مبلغ ده هزار تومان، واریز شده در تاریخ..."
* **RTL Behaviour:** Logo and Name align on the Right. Values, statuses, and time values align on the Left.

### 2.9 Quick Actions
* **Purpose:** Fast-access actions associated with transaction results without entering detail views.
* **Visual Priority:** Level 2.
* **Placement:** Slide-to-reveal underlying horizontal action block (accessible on swipe left on a row).
* **Spacing:** Target sizing: `56 × 56 dp` per icon button.
* **Typography:** Vazirmatn Medium, `11 sp` under icon.
* **Icons:** `star_border`/`star` (Favorite), `share` (Share Transaction receipt), `label` (Edit tags).
* **Elevation:** Level 1.
* **States:** Fully interactive with instant visual state validation.
* **RTL Behaviour:** Revealed on sliding from left to right (since slide-to-reveal actions are reversed in RTL context).

### 2.10 Empty Search State
* **Purpose:** Educate users on search features when zero keywords are typed.
* **Visual Priority:** Level 3.
* **Placement:** Centered perfectly within the main scrolling body.
* **Spacing:** Graphic illustration height: `160 dp`. Vertical margin to sub-text: `16 dp`.
* **Typography:** Title: Vazirmatn SemiBold, `16 sp`. Sub-title: Vazirmatn Regular, `14 sp`.
* **Icons:** Subtle system outline illustration showing card search metrics.
* **Elevation:** Level 0.
* **RTL Behaviour:** Centered perfectly across all aspect ratios.

### 2.11 No Result State
* **Purpose:** Gracefully alert the user that nothing matched their query and offer adjustment paths.
* **Visual Priority:** Level 2.
* **Placement:** Centered in the main scrolling body.
* **Spacing:** Same spacing profile as Empty Search. Includes a clear action button (`48 dp` tall).
* **Typography:** Vazirmatn Medium, `15 sp`.
* **Icons:** `search_off` / detailed custom visual illustration.
* **Elevation:** Level 0.
* **States:** Active "پاک کردن فیلترها" (Clear Filters) primary button.

### 2.12 Loading State
* **Purpose:** Inform the user that a complex offline search index lookup is actively executing.
* **Visual Priority:** Level 1 (Interruption block).
* **Placement:** Inside the result list window.
* **Spacing:** Shimmer skeletons replacing real rows with height matching actual items (`72 dp`).
* **Elevation:** Level 0 (Shimmer overlay).
* **Animation:** Pulse animation cycle (interval `1500 ms`) flowing right to left.

### 2.13 Offline State
* **Purpose:** Clearly flag that data being searched is entirely offline and secure.
* **Visual Priority:** Level 1.
* **Placement:** Non-intrusive status banner below the App Bar or subtle inline text.
* **Typography:** Vazirmatn Medium, `12 sp`.
* **Icons:** `cloud_off` or `security`.
* **Elevation:** Level 0.

---

## 3. Advanced Filtering Rules & Controls

The advanced filter panel allows precise logical isolation of transactions.

| Filter Category | Control Component | Value Range / Options | RTL Layout Details |
| :--- | :--- | :--- | :--- |
| **Keyword** | Embedded text input field | Any Persian / alphanumeric string | Right-aligned prompt |
| **Bank Selection** | Horizontal scrollable avatar track | Multi-select circular avatars with bank logos | Starts from Right margin |
| **Transaction Type** | Segmented Pill Controls | Income (درآمد) / Expense (هزینه) / Purchase (خرید) / Transfer (انتقال) | Horizontal segment row RTL |
| **Amount Range** | Dual Range Slider | Min (0) to Max (500M+ IRR) | Low value on Right, high value on Left |
| **Date Range** | Visual Calendar (Custom Date) | Jalali Calendar interface (امروز / این هفته / این ماه / بازه دلخواه) | Grid calendar RTL flow |
| **Has Note / No Note**| Segmented switch toggle | Match options with notes/without | Switch placement Left-aligned |
| **Favorites** | Toggle switch | True / False | Switch placement Left-aligned |
| **Tags** | Flexible tag cloud wraps | Dynamic system tags selected by chip | Auto-wrapping flow starting Right |
| **Archived (Future)**| Toggle switch (grayed out) | Reserved placeholder | Disabled visual styling |
| **Currency (Future)** | Dropdown selection | Reserved placeholder | Disabled visual styling |

---

## 4. Search Experience & Optimization Architecture

To handle tens of thousands of transactions instantaneously, the design specifies the following debouncing, indexing, and highlighting behaviors:

```
[User Input Event]
       │
       ▼
[Debounce Window (250ms)]
       │
       ├─► (If character count < 2) ──► Keep history & suggestion view active
       │
       ▼ (Character count >= 2)
[Execute Fast Index Query (Offline First)]
       │
       ├─► Highlight matched sub-strings in UI results
       └─► Return result dataset to Transaction Feed
```

* **Live & Debounced Search:** Search queries are not evaluated on every individual character keypress. Instead, a **250ms debounce window** is applied. If the input remains static for 250ms, the local SQL/NoSQL index is hit. This maintains high frames-per-second (FPS) and battery efficiency on older Android/iOS devices.
* **Search Suggestions:** Displays dynamically updating suggestions based on matches found within beneficiary names and category tags.
* **Highlight Matching Text:** Substrings within the transaction result row that match the user’s active query are styled with a distinct text background (`#705D00` with 15% opacity) to speed up recognition.
* **Search History Management:** Up to 10 historical search items are preserved. A prominent, easily accessible "پاک کردن تاریخچه" (Clear History) button is located at the bottom-left of the history list.

---

## 5. Result Layout & Typography (Vazirmatn Specs)

Every transaction row item is meticulously spaced to support maximum glanceability.

```
+-------------------------------------------------------------------------------+
| [Favorite]   [Amount: +1,200,000 ریال]          [Bank Name]  [Bank Logo (36dp)]|
|   (Left)     (Primary Bold Teal)                (Vazir Medium)   (Right)      |
|                                                                               |
| [Time: 14:32]   [Matched Substring Highlight]                     [Note Icon] |
|   (Left)        (Secondary Slate Style)                            (Right)    |
+-------------------------------------------------------------------------------+
```

### Visual Specifications
* **Row Dimensions:** Height = `72 dp`, Width = Full Screen minus side padding (`32 dp` total).
* **Bank Logo:** Size: `36 × 36 dp`, border radius: `18 dp` (perfect circle). Background tint is styled based on the bank's color brand with a secure dynamic container opacity overlay.
* **Amount Formatting:** Positive numbers display with a leading green sign (`+`) using color `#006A60`. Negative numbers display with a red sign (`-`) using color `#BA1A1A`. Format matches standard RTL Persian notation: `ریال ۱,۲۰۰,۰۰۰`.

---

## 6. Interaction Flows & Screen States

### 6.1 State Transitions Flowchart

```
                 ┌───────────────────────────┐
                 │    Idle State (Initial)   │
                 └─────────────┬─────────────┘
                               │ User types >= 2 characters
                               ▼
                 ┌───────────────────────────┐
                 │       Debounce Stage      │
                 └─────────────┬─────────────┘
                               │ 250ms timeout expires
                               ▼
                 ┌───────────────────────────┐
                 │       Loading State       │
                 │    (Shimmer skeleton rows)│
                 └─────────────┬─────────────┘
                               │
               ┌───────────────┴───────────────┐
               ▼                               ▼
     [ Results Found ]               [ No Results Found ]
               │                               │
               ▼                               ▼
┌─────────────────────────────┐ ┌─────────────────────────────┐
│    Transaction Results      │ │      No Results View        │
│    (Highlight Matches)      │ │   (Filter Reset Options)    │
└─────────────────────────────┘ └─────────────────────────────┘
```

### 6.2 UX Component State Details

1. **Loading State:** Skeletons mimic the dual-line card structure. A glowing wave gradient mask sweeps across cards from right to left with a duration of `1200 ms`.
2. **Error State:** Triggers if the offline storage is corrupted or encounters reading delays. Uses the MD3 `Error` color token `#BA1A1A`, containing an illustration and an "امکان تلاش مجدد" (Retry) action trigger.
3. **Offline State:** An inline micro-status chip is nestled into the bottom-right of the search area indicating that searches are computed offline and securely stored within the local sandbox.

---

## 7. Accessibility (a11y) & RTL Architecture

### RTL (Right-to-Left) Localization Review
* **Mirroring Principles:** All screen elements mirror horizontally. Buttons, progress paths, sliders, and navigation lists read and swipe from right to left.
* **Numeral Standards:** Persian numerals (`۱۲۳۴۵۶۷۸۹۰`) are fully used in amount displaying and chronological logs to respect regional formatting parameters.
* **Text Alignments:** Standard text alignments default to `TextAlign.right` for titles, notes, and search suggestions, and `TextAlign.left` for timestamp data on the far left.

### Accessibility Standards Compliance
* **Interactive Targets:** Interactive touch targets are never below `48 × 48 dp`.
* **Contrast Ratios:** Text color combinations yield a contrast ratio of at least **4.5:1** against backgrounds (meeting WCAG 2.1 AA requirements). Primary brand colors satisfy **7:1** ratios.
* **Screen Reader Semantic Semiotics:** Form elements explicitly declare descriptive tags (`Semantics` in Flutter, `contentDescription` on native Android). Complex rows group elements into single screen reader chunks to prevent jarring disjointed audio queues.

---

## 8. UX Validation Checklist

Before initiating developer integration or building dynamic Flutter layouts, verify the UI matches these core principles:

- [ ] Does every interactive surface (chip, text input, close button) meet the **48 dp** touch boundary?
- [ ] Is the typing debounce value set to **250ms** to avoid continuous backend/database UI stuttering?
- [ ] Are Persian numeric representations aligned properly in RTL and structured chronologically?
- [ ] Does the visual highlight mechanism pinpoint matched substrings using `#705D00` accent overlays?
- [ ] Are loading animations styled via non-obtrusive, right-to-left flowing shimmer blocks?
- [ ] Do design assets avoid using raw dropshadow values, relying on standard MD3 surface tint overrides instead?
- [ ] Is the voice search button distinctly styled as a future placeholder to avoid user path confusion?

---
*End of Design Specification. Designed for BankYar Mobile Applications under secure personal fintech system paradigms.*
