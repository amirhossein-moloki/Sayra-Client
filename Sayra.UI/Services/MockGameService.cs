using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Sayra.UI.Models;

namespace Sayra.UI.Services
{
    public class MockGameService
    {
        public static List<GameItem> GetStaticGames()
        {
            return new List<GameItem>
            {
                // AAA Games
                new GameItem
                {
                    Id = "1",
                    Title = "Cyberpunk 2077",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "CD PROJEKT RED",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "سایبرپانک ۲۰۷۷ یک بازی اکشن و نقش‌آفرینی در جهان باز است که در کلان‌شهر نایت سیتی رخ می‌دهد؛ جایی که قدرت، تجمل و اصلاحات بدنی حرف اول را می‌زنند."
                },
                new GameItem
                {
                    Id = "2",
                    Title = "Red Dead Redemption 2",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1174180/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1174180/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1174180/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Rockstar Games",
                    ReleaseYear = "2018",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "داستان حماسی آرتور مورگان و گروه ون در لیند در اواخر دوران غرب وحشی آمریکا. یک اثر هنری بی‌نظیر از راک‌استار گیمز با جزئیات خیره‌کننده."
                },
                new GameItem
                {
                    Id = "3",
                    Title = "GTA V",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/271590/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/271590/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/271590/library_hero.jpg",
                    Launcher = "Epic",
                    Developer = "Rockstar Games",
                    ReleaseYear = "2013",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "ماجراجویی سه جنایتکار کاملاً متفاوت در شهر لوس سانتوس؛ یکی از پرفروش‌ترین و محبوب‌ترین بازی‌های تاریخ سینما و بازی‌های ویدیویی."
                },
                new GameItem
                {
                    Id = "4",
                    Title = "Elden Ring",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1245620/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1245620/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1245620/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "FromSoftware",
                    ReleaseYear = "2022",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "برخیز ای تارنیشد! با هدایت موهبت و به دست گرفتن قدرت الدن رینگ، به فرمانروای الدن در سرزمین‌های میانی تبدیل شو."
                },
                new GameItem
                {
                    Id = "5",
                    Title = "Hogwarts Legacy",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/990080/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/990080/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/990080/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Avalanche Software",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک بازی نقش‌آفرینی اکشن و جهان باز در دنیای جادویی هری پاتر. میراث خود را بسازید و اسرار پنهان دنیای جادوگری را کشف کنید."
                },
                new GameItem
                {
                    Id = "6",
                    Title = "The Witcher 3: Wild Hunt",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/292030/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/292030/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/292030/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "CD PROJEKT RED",
                    ReleaseYear = "2015",
                    Status = "Locked",
                    IsAvailable = false,
                    IsSelected = false,
                    Description = "شما گرالت از ریویا هستید، یک هیولاکش حرفه‌ای در دنیایی فانتزی و تاریک که در آن باید فرزند تقدیر را پیدا کنید."
                },
                new GameItem
                {
                    Id = "7",
                    Title = "Starfield",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1716740/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1716740/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1716740/library_hero.jpg",
                    Launcher = "Xbox",
                    Developer = "Bethesda Game Studios",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "اولین جهان کاملاً جدید بتسدا پس از ۲۵ سال؛ سفری حماسی به اعماق فضا و کاوش در میان بیش از هزار سیاره متمایز."
                },
                new GameItem
                {
                    Id = "8",
                    Title = "Forza Horizon 5",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1551360/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1551360/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1551360/library_hero.jpg",
                    Launcher = "Xbox",
                    Developer = "Playground Games",
                    ReleaseYear = "2021",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "سفری مهیج به سرزمین‌های پهناور و زیبای مکزیک با تنوع آب‌وهوایی فوق‌العاده و صدها خودروی سوپر اسپرت برتر جهان."
                },
                new GameItem
                {
                    Id = "9",
                    Title = "Assassin's Creed Mirage",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2892180/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2892180/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2892180/library_hero.jpg",
                    Launcher = "Ubisoft",
                    Developer = "Ubisoft Bordeaux",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "سفر به بغداد قرن نهم و تجربه داستان حماسی باسم؛ بازگشت به ریشه‌های اصیل پارکور و مخفی‌کاری در این فرانچایز محبوب."
                },
                new GameItem
                {
                    Id = "10",
                    Title = "Assassin's Creed Valhalla",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2208920/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2208920/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2208920/library_hero.jpg",
                    Launcher = "Ubisoft",
                    Developer = "Ubisoft Montreal",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "در نقش ایوور، رهبر وایکینگ‌ها، به انگلستان هجوم ببرید و قبیله خود را به سمت شکوه و عظمت هدایت کنید."
                },

                // FPS / Shooter Games
                new GameItem
                {
                    Id = "11",
                    Title = "Counter-Strike 2",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/730/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/730/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/730/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Valve",
                    ReleaseYear = "2023",
                    Status = "Currently Playing",
                    IsAvailable = true,
                    IsSelected = true, // Selected by default
                    Description = "نسل بعدی رقابت‌های تاکتیکی و شوتر اول شخص تاریخ‌ساز جهان؛ بازسازی کامل نقشه با موتور قدرتمند Source 2."
                },
                new GameItem
                {
                    Id = "12",
                    Title = "Valorant",
                    Genre = "Shooter",
                    ImagePath = "https://cdn2.unrealengine.com/egs-valorant-riotgames-s2-1200x1600-244342279024.jpg",
                    LogoImage = "https://cdn2.unrealengine.com/egs-valorant-riotgames-ic1-400x400-946761595952.png",
                    BackgroundImage = "https://cdn2.unrealengine.com/egs-valorant-riotgames-g1a-00-1920x1080-60b643a6d96a.jpg",
                    Launcher = "Riot",
                    Developer = "Riot Games",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "ترکیبی بی‌نظیر از تیراندازی تاکتیکی ۵ به ۵ و توانایی‌های جادویی منحصر‌به‌فرد مأموران ویژه در ریوت گیمز."
                },
                new GameItem
                {
                    Id = "13",
                    Title = "Call of Duty: MW III",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Sledgehammer Games",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "نبردی تمام‌عیار در نقش کاپیتان پرایس در برابر تهدید جهانی ولادیمیر ماکاروف در بخش داستانی و چندنفره نمادین."
                },
                new GameItem
                {
                    Id = "14",
                    Title = "Call of Duty: Warzone",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/library_600x900_2x.jpg", // Warzone shares same client app ID or similar layout
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1938090/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Infinity Ward",
                    ReleaseYear = "2020",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بتل رویال عظیم، رایگان و فوق‌العاده هیجان‌انگیز از سری کال آف دیوتی با نبردهای تن به تن نفس‌گیر ۱۵۰ نفره."
                },
                new GameItem
                {
                    Id = "15",
                    Title = "Battlefield 2042",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1517290/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1517290/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1517290/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "DICE",
                    ReleaseYear = "2021",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "نبردهای همه‌جانبه نظامی با پشتیبانی از ۱۲۸ بازیکن؛ تغییرات اقلیمی شدید در نقشه‌ها و فناوری‌های جنگی آینده."
                },
                new GameItem
                {
                    Id = "16",
                    Title = "Rainbow Six Siege",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/359550/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/359550/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/359550/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Ubisoft Montreal",
                    ReleaseYear = "2015",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک شوتر تاکتیکی بی‌نظیر که بر پایه تخریب‌پذیری بالای محیط، کار تیمی دقیق و استراتژی‌های دفاع و نفوذ بنا شده است."
                },
                new GameItem
                {
                    Id = "17",
                    Title = "Apex Legends",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172470/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172470/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172470/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "Respawn Entertainment",
                    ReleaseYear = "2019",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بتل رویال پرسرعت و قهرمان‌محور استودیو ریسپاون؛ کاراکترهای منحصربه‌فرد با توانایی‌های ویژه برای نبردهای گروهی سه‌نفره."
                },
                new GameItem
                {
                    Id = "18",
                    Title = "Overwatch 2",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/233320/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/233320/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/233320/library_hero.jpg",
                    Launcher = "Battle.net",
                    Developer = "Blizzard Entertainment",
                    ReleaseYear = "2022",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "تکامل رقابت‌های تیمی ۵ به ۵ بلیزارد با قهرمانان جدید، حالت‌های بازی متنوع و گرافیک به‌روزشده."
                },
                new GameItem
                {
                    Id = "19",
                    Title = "DOOM Eternal",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/782330/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/782330/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/782330/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "id Software",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "ارتش‌های جهنم به زمین هجوم آورده‌اند. در نقش دوم اسلیر، آنها را با سرعت بالا و ابزارهای مرگبار تکه‌تکه کنید."
                },

                // Survival Games
                new GameItem
                {
                    Id = "20",
                    Title = "Rust",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/252490/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/252490/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/252490/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Facepunch Studios",
                    ReleaseYear = "2018",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "تنها هدف در راست بقاست؛ غلبه بر گرسنگی، سرما و سایر بازیکنان وحشی در یک نقشه چندنفره گسترده."
                },
                new GameItem
                {
                    Id = "21",
                    Title = "ARK: Survival Ascended",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/239030/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/239030/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/239030/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Studio Wildcard",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بازسازی نسل بعدی دنیای بقا با دایناسورها با استفاده از Unreal Engine 5؛ رام کردن، سوارکاری و نبرد در دنیای وحشی."
                },
                new GameItem
                {
                    Id = "22",
                    Title = "Sons of the Forest",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1326470/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1326470/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1326470/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Endnight Games",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "برای نجات یک میلیاردر مفقود شده به جزیره‌ای آدمخوار فرستاده می‌شوید. بسازید، بجنگید و برای زنده ماندن تلاش کنید."
                },
                new GameItem
                {
                    Id = "23",
                    Title = "Subnautica",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/264710/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/264710/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/264710/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Unknown Worlds",
                    ReleaseYear = "2018",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "سقوط در اعماق اقیانوس یک سیاره بیگانه غریبه؛ کاوش در میان صخره‌های مرجانی زیبا و غارهای آبی اسرارآمیز."
                },
                new GameItem
                {
                    Id = "24",
                    Title = "Minecraft",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg", // Minecraft high quality cover placeholder
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_hero.jpg",
                    Launcher = "Xbox",
                    Developer = "Mojang Studios",
                    ReleaseYear = "2011",
                    Status = "Installed",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بزرگترین بازی سندباکس و خلاقانه تاریخ؛ ساختن آثار هنری شگفت‌انگیز بلاک به بلاک و زنده ماندن در طول شب‌های مخوف."
                },

                // Racing Games
                new GameItem
                {
                    Id = "25",
                    Title = "Need for Speed Unbound",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1374300/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1374300/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1374300/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "Criterion Games",
                    ReleaseYear = "2022",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "آخرین نسخه از سری نمادین نید فور اسپید با گرافیک هنری منحصربه‌فرد انیمه‌ای، مسابقات خیابانی و شخصی‌سازی دیوانه‌وار."
                },
                new GameItem
                {
                    Id = "26",
                    Title = "Need for Speed Heat",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1222680/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1222680/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1222680/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "Ghost Games",
                    ReleaseYear = "2019",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "رقابت‌های پرحرارت روزانه و مسابقات خیابانی و غیرقانونی شبانه زیر باران‌های کلان‌شهر پالم سیتی."
                },
                new GameItem
                {
                    Id = "27",
                    Title = "Assetto Corsa",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/244210/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/244210/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/244210/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Kunos Simulazioni",
                    ReleaseYear = "2014",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یکی از شبیه‌سازهای برتر رانندگی جهان با فیزیک فوق‌العاده واقع‌گرایانه و پشتیبانی گسترده از مادهای شخصی‌سازی."
                },
                new GameItem
                {
                    Id = "28",
                    Title = "The Crew Motorfest",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/library_600x900_2x.jpg", // Fallback library image
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/library_hero.jpg",
                    Launcher = "Ubisoft",
                    Developer = "Ivory Tower",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "جشنواره اتومبیل‌رانی فوق‌العاده در جزیره زیبای اوآهو هاوایی؛ رانندگی با نمادین‌ترین خودروهای کلاسیک و مدرن جهان."
                },
                new GameItem
                {
                    Id = "29",
                    Title = "F1 24",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2488620/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "Codemasters",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بازی رسمی مسابقات فرمول یک قهرمانی جهان با سیستم هندلینگ جدید و رانندگان و تیم‌های واقعی فصل ۲۰۲۴."
                },

                // Sports Games
                new GameItem
                {
                    Id = "30",
                    Title = "EA SPORTS FC 25",
                    Genre = "Sports",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2669320/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2669320/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2669320/library_hero.jpg",
                    Launcher = "EA",
                    Developer = "EA Vancouver",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "شبیه‌ساز پیشرو فوتبال جهان با قابلیت‌های تکنولوژی Rush و شبیه‌سازی دقیق تاکتیکی بازیکنان فوتبال."
                },
                new GameItem
                {
                    Id = "31",
                    Title = "NBA 2K25",
                    Genre = "Sports",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2878980/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2878980/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2878980/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Visual Concepts",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "حکومت بر زمین بسکتبال در حالت‌های MyCAREER و MyTEAM با گیم‌پلی پیشرفته ProPLAY و واقع‌گرایی بی‌نظیر."
                },
                new GameItem
                {
                    Id = "32",
                    Title = "WWE 2K24",
                    Genre = "Sports",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2315690/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2315690/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2315690/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Visual Concepts",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بخش داستانی ۴۰ سالگی رسلمنیا به همراه بزرگترین ستاره‌های دنیای کشتی کج و مسابقات حماسی قفس."
                },

                // Strategy Games
                new GameItem
                {
                    Id = "33",
                    Title = "Civilization VI",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/289070/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/289070/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/289070/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Firaxis Games",
                    ReleaseYear = "2016",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "تمدنی نوین برپا کنید که در گذر زمان پایدار بماند؛ بزرگترین بازی استراتژیک نوبتی با وسعت فوق‌العاده."
                },
                new GameItem
                {
                    Id = "34",
                    Title = "Age of Empires IV",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1466860/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1466860/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1466860/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Relic Entertainment",
                    ReleaseYear = "2021",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بازگشت یکی از شاهکارهای محبوب دنیای استراتژیک همزمان؛ رهبری تمدن‌های بزرگ تاریخی در نبردهای عظیم قرون وسطی."
                },
                new GameItem
                {
                    Id = "35",
                    Title = "Total War: WH III",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Creative Assembly",
                    ReleaseYear = "2022",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "پایان حماسی این سه‌گانه فانتزی تاریک؛ ارتش خود را در دنیای پرآشوب وارهمر بسیج کرده و قلمروهای شیاطین را فتح کنید."
                },
                new GameItem
                {
                    Id = "36",
                    Title = "StarCraft II",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/library_600x900_2x.jpg", // Fallback strategy cover
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1142710/library_hero.jpg",
                    Launcher = "Battle.net",
                    Developer = "Blizzard Entertainment",
                    ReleaseYear = "2010",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "شاهکار رقابت‌های استراتژیک همزمان بلیزارد؛ جنگ‌های کهکشانی میان سه نژاد بزرگ تراست، پروتوس و زرگ."
                },

                // Indie / Popular Games
                new GameItem
                {
                    Id = "37",
                    Title = "Hades",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1145360/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1145360/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1145360/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Supergiant Games",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک بازی روگ‌لایک هک اند اسلش تحسین‌شده؛ فرار شاهزاده دنیای زیرزمین یونان از چنگال پدرش هادس."
                },
                new GameItem
                {
                    Id = "38",
                    Title = "Hollow Knight",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/367520/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/367520/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/367520/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Team Cherry",
                    ReleaseYear = "2017",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک ماجراجویی کلاسیک دوبعدی به سبک مترویدوانیا در اعماق قلمروی باستانی حشرات هالوونست."
                },
                new GameItem
                {
                    Id = "39",
                    Title = "Dead Cells",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/588650/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/588650/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/588650/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Motion Twin",
                    ReleaseYear = "2018",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "ترکیب چالش‌برانگیز سبک‌های روگ‌لایک و مترویدوانیا در قالب یک قلعه تاریک همیشه در حال تغییر."
                },
                new GameItem
                {
                    Id = "40",
                    Title = "Terraria",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/105600/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Re-Logic",
                    ReleaseYear = "2011",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بکاوید، بسازید و بجنگید! دنیای پهناور و بی‌پایان دوبعدی که پتانسیل‌های بی‌شماری را برای خلاقیت به شما ارائه می‌دهد."
                },
                new GameItem
                {
                    Id = "41",
                    Title = "Cuphead",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/268910/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/268910/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/268910/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Studio MDHR",
                    ReleaseYear = "2017",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک بازی اکشن تیراندازی ران اند گان به همراه باس‌فایت‌های نفس‌گیر با سبک انیمیشن‌های نمادین دهه ۱۹۳۰ میلادی."
                },
                new GameItem
                {
                    Id = "42",
                    Title = "Stardew Valley",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/413150/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/413150/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/413150/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "ConcernedApe",
                    ReleaseYear = "2016",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "زندگی رویایی مزرعه‌داری در یک دهکده آرام؛ کاشت محصولات کشاورزی، پرورش دام و برقراری ارتباط با ساکنین دوست‌داشتنی شهر."
                },

                // Additional Popular Games for Library Richness (Total to 61)
                new GameItem
                {
                    Id = "43",
                    Title = "Baldur's Gate 3",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1086940/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1086940/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1086940/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Larian Studios",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "برنده جایزه بهترین بازی سال ۲۰۲۳؛ ماجراجویی نقش‌آفرینی همه‌جانبه با آزادی عمل فوق‌العاده در جهان فراموش‌شده سیاهچال‌ها و اژدهایان."
                },
                new GameItem
                {
                    Id = "44",
                    Title = "Dota 2",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/570/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/570/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/570/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Valve",
                    ReleaseYear = "2013",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "رقابت‌های آنلاین حماسی MOBA؛ نبرد مستمر دو تیم ۵ نفره برای نابودی قلعه باستانی حریف با صدها قهرمان متمایز."
                },
                new GameItem
                {
                    Id = "45",
                    Title = "PUBG: BATTLEGROUNDS",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/578080/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/578080/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/578080/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "KRAFTON, Inc.",
                    ReleaseYear = "2017",
                    Status = "Unavailable",
                    IsAvailable = false,
                    IsSelected = false,
                    Description = "پدر سبک بتل رویال نوین؛ در جزیره فرود بیایید، لوت کنید و تا آخرین نفس برای بقا بجنگید."
                },
                new GameItem
                {
                    Id = "46",
                    Title = "Destiny 2",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1085660/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1085660/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1085660/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Bungie",
                    ReleaseYear = "2017",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک شوتر آنلاین MMO حماسی و تخیلی از سازندگان هالو؛ به پاسداران نور بپیوندید و در سراسر منظومه شمسی مبارزه کنید."
                },
                new GameItem
                {
                    Id = "47",
                    Title = "Helldivers 2",
                    Genre = "Shooter",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/553850/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/553850/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/553850/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Arrowhead Game Studios",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "شوتر گروهی و سوم شخص پرفروش سال ۲۰۲۴؛ برای آزادی و دموکراسی هدایت‌شده در برابر هجوم بیگانگان فضایی بجنگید."
                },
                new GameItem
                {
                    Id = "48",
                    Title = "God of War",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1593500/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1593500/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1593500/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Santa Monica Studio",
                    ReleaseYear = "2022",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "داستان سفر حماسی و احساسی کریتوس و آترئوس به قلمروهای اساطیر نورس؛ شاهکار مطلق سونی برای پی سی."
                },
                new GameItem
                {
                    Id = "49",
                    Title = "Alan Wake 2",
                    Genre = "Action-Adventure",
                    ImagePath = "https://cdn2.unrealengine.com/egs-alanwake2-remedyentertainment-s2-1200x1600-47d0c3eb1a3c.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/logo.png", // Fallback logo
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/library_hero.jpg",
                    Launcher = "Epic",
                    Developer = "Remedy Entertainment",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک تریلر روانشناختی و بقا-وحشت خیره‌کننده با دو داستان موازی الن ویک نویسنده و ساگا اندرسون کارآگاه FBI."
                },
                new GameItem
                {
                    Id = "50",
                    Title = "Diablo IV",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2344520/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2344520/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2344520/library_hero.jpg",
                    Launcher = "Battle.net",
                    Developer = "Blizzard Entertainment",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "نبردی بی‌پایان در برابر ملکه تاریکی لیلیت در دنیای سیاه پناهگاه (Sanctuary) با کلاس‌های قهرمانی قدرتمند."
                },
                new GameItem
                {
                    Id = "51",
                    Title = "Ghost of Tsushima",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2215430/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2215430/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2215430/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Sucker Punch Productions",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "سفری شگفت‌انگیز به ژاپن فئودال و جزیره سوشیما؛ دفاع شرافتمندانه جین ساکای سامورایی در برابر ارتش مغول‌ها."
                },
                new GameItem
                {
                    Id = "52",
                    Title = "Monster Hunter: World",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/582010/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/582010/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/582010/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "CAPCOM Co., Ltd.",
                    ReleaseYear = "2018",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "شکار هیولاهای عظیم‌الجثه در اکوسیستم‌های پویا و ساخت تجهیزات و سلاح‌های قدرتمند افسانه‌ای از پوست و استخوان آنها."
                },
                new GameItem
                {
                    Id = "53",
                    Title = "Slay the Spire",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/646570/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/646570/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/646570/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Mega Crit Games",
                    ReleaseYear = "2019",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "تلفیقی بی‌نقص از بازی‌های کارتی تاکتیکی و روگ‌لایک؛ دسته کارت خود را به بهترین شکل ارتقا دهید و برج را فتح کنید."
                },
                new GameItem
                {
                    Id = "54",
                    Title = "Lethal Company",
                    Genre = "Indie",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1966720/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1966720/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1966720/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Zeekerss",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک بازی چندنفره کوآپ ترسناک و فوق‌العاده خنده‌دار؛ جمع‌آوری ضایعات آهن در ماه‌های متروکه صنعتی فضایی."
                },
                new GameItem
                {
                    Id = "55",
                    Title = "Palworld",
                    Genre = "Survival",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1623730/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1623730/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1623730/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Pocketpair",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "معروف به پوکمون مسلح؛ کار و تلاش با موجودات دوست‌داشتنی پال، ساخت پایگاه و نبردهای اکشن هیجان‌انگیز."
                },
                new GameItem
                {
                    Id = "56",
                    Title = "Factorio",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/427520/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/427520/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/427520/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Wube Software",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "ساخت و مدیریت کارخانه‌های صنعتی فوق‌العاده پیچیده و اتوماتیک بر روی یک سیاره بیگانه متخاصم."
                },
                new GameItem
                {
                    Id = "57",
                    Title = "Black Myth: Wukong",
                    Genre = "RPG",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2358720/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2358720/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2358720/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Game Science",
                    ReleaseYear = "2024",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "یک ماجراجویی خیره‌کننده و اکشن نقش‌آفرینی بر اساس اساطیر سنتی چین و رمان حماسی سفر به باختر."
                },
                new GameItem
                {
                    Id = "58",
                    Title = "Resident Evil 4",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2050650/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2050650/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2050650/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "CAPCOM Co., Ltd.",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بازسازی مدرن و بی‌نقص شاهکار ترس و بقای سال ۲۰۰۵؛ نجات دختر رئیس‌جمهور توسط مامور ویژه لئون اس کندی."
                },
                new GameItem
                {
                    Id = "59",
                    Title = "Sea of Thieves",
                    Genre = "Action-Adventure",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172620/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172620/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1172620/library_hero.jpg",
                    Launcher = "Xbox",
                    Developer = "Rare",
                    ReleaseYear = "2020",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "تجربه واقعی دزدی دریایی با دوستان؛ دریانوردی, نبرد, اکتشاف جزایر و غارت گنجینه‌های مدفون افسانه‌ای."
                },
                new GameItem
                {
                    Id = "60",
                    Title = "Cities: Skylines II",
                    Genre = "Strategy",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/949230/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/949230/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/949230/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "Colossal Order",
                    ReleaseYear = "2023",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "بزرگترین و عمیق‌ترین شبیه‌ساز شهرسازی تاریخ؛ طراحی، احداث و مدیریت یک کلان‌شهر فوق‌العاده با زیرساخت‌های واقعی."
                },
                new GameItem
                {
                    Id = "61",
                    Title = "Euro Truck Simulator 2",
                    Genre = "Racing",
                    ImagePath = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/227300/library_600x900_2x.jpg",
                    LogoImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/227300/logo.png",
                    BackgroundImage = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/227300/library_hero.jpg",
                    Launcher = "Steam",
                    Developer = "SCS Software",
                    ReleaseYear = "2012",
                    Status = "Available",
                    IsAvailable = true,
                    IsSelected = false,
                    Description = "رانندگی با کامیون‌های سنگین در جاده‌های سرسبز اروپا؛ ایجاد شرکت حمل‌ونقل و باربری در کشورهای مختلف."
                }
            };
        }

        public async Task<ObservableCollection<GameItem>> GetGamesAsync()
        {
            // Simulate realistic API latency
            await Task.Delay(150);
            return new ObservableCollection<GameItem>(GetStaticGames());
        }
    }
}
