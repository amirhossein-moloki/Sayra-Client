# دستورالعمل گام‌به‌گام بازسازی چیدمان و رنگ‌های اصلی صفحه جزئیات بازی
## (Layout and Color Reconstruction Guide)

این راهنما برای اعمال دستی تغییرات جهت بازگرداندن چیدمان دو ستونه/دو ردیفه متقارن اصلی و رنگ زرد پرمیوم بازی‌ها طراحی شده است. با انجام این مراحل، مشکل چیدمان عمودی کامپوننت‌ها حل شده و تصویر پس‌زمینه و لوگوی بازی دقیقاً در جای اصلی خود قرار می‌گیرند.

---

### بخش ۱: اصلاح رنگ‌ها و درخشش دکمه‌ها در تم سنترال
فایل تم اختصاصی جزئیات بازی را باز کرده و کدهای زیر را جایگزین کنید تا رنگ زرد و بک‌دراپ اصلی بازی‌ها بازیابی شود:

**مسیر فایل:** `Sayra.UI/Themes/GameDetailTheme.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:c="clr-namespace:Sayra.UI.Controls">
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Colors.xaml" />
        <ResourceDictionary Source="Brushes.xaml" />
        <ResourceDictionary Source="Styles.xaml" />
    </ResourceDictionary.MergedDictionaries>

    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />

    <!-- رنگ‌های پس‌زمینه عمیق اصلی و رنگ زرد برجسته بازی‌ها -->
    <Color x:Key="GameDetail.Background.Color">#08090D</Color>
    <Color x:Key="GameDetail.Highlight.Color">#ffff3d</Color>
    <Color x:Key="GameDetail.Highlight.HoverColor">#f4f46b</Color>
    <Color x:Key="GameDetail.CardBackground.Color">#CC1F1F23</Color>

    <!-- مپ کردن براش‌ها برای هماهنگی با کدهای اصلی -->
    <SolidColorBrush x:Key="GameDetail.Background" Color="{DynamicResource GameDetail.Background.Color}" />
    <SolidColorBrush x:Key="GameDetail.CardBackground" Color="{DynamicResource GameDetail.CardBackground.Color}" />
    <SolidColorBrush x:Key="GameDetail.PanelBackground" Color="{DynamicResource GameDetail.CardBackground.Color}" />
    <SolidColorBrush x:Key="GameDetail.Border" Color="{DynamicResource App.Border}" />
    <SolidColorBrush x:Key="GameDetail.Highlight" Color="{DynamicResource GameDetail.Highlight.Color}" />
    <SolidColorBrush x:Key="GameDetail.PriceBackground" Color="{DynamicResource App.Surface}" />
    <SolidColorBrush x:Key="GameDetail.ButtonPrimary" Color="{DynamicResource GameDetail.Highlight.Color}" />
    <SolidColorBrush x:Key="GameDetail.TextPrimary" Color="{DynamicResource App.Text.Primary}" />
    <SolidColorBrush x:Key="GameDetail.TextSecondary" Color="{DynamicResource App.Text.Secondary}" />

    <!-- افکت گرادیان کلی پنجره روی بک‌دراپ تصویر محو شده -->
    <LinearGradientBrush x:Key="GameDetail.HeroOverlay" StartPoint="0.5,0" EndPoint="0.5,1">
        <GradientStop Color="#9908090D" Offset="0.0" />
        <GradientStop Color="#F308090D" Offset="0.65" />
        <GradientStop Color="#FF08090D" Offset="1.0" />
    </LinearGradientBrush>

    <!-- براش‌های افکت هاور دکمه شیشه‌ای برگشت -->
    <SolidColorBrush x:Key="GameDetail.Button.HoverBg" Color="#1AFFFFFF" />
    <SolidColorBrush x:Key="GameDetail.Button.HoverBorder" Color="#80FFFFFF" />
    <SolidColorBrush x:Key="GameDetail.Button.PressedBg" Color="#33FFFFFF" />

    <SolidColorBrush x:Key="GameDetail.Button.DangerHoverBg" Color="{DynamicResource App.Danger}" Opacity="0.1" />
    <SolidColorBrush x:Key="GameDetail.Button.DangerPressedBg" Color="{DynamicResource App.Danger}" Opacity="0.2" />

    <!-- سایر متادیتاها و وضعیت‌ها -->
    <SolidColorBrush x:Key="GameDetail.CoverPlaceholderBackground" Color="#10FFFFFF" />
    <SolidColorBrush x:Key="GameDetail.GamepadIconFill" Color="#33FFFFFF" />
    <SolidColorBrush x:Key="GameDetail.BadgeBackground" Color="#12FFFFFF" />

    <SolidColorBrush x:Key="GameDetail.Status.SuccessBackground" Color="#1014BE78" />
    <SolidColorBrush x:Key="GameDetail.Status.SuccessBorder" Color="{DynamicResource App.Success}" />
    <SolidColorBrush x:Key="GameDetail.Status.DangerBackground" Color="#10F46B6B" />
    <SolidColorBrush x:Key="GameDetail.Status.DangerBorder" Color="{DynamicResource App.Danger}" />
    <SolidColorBrush x:Key="GameDetail.Status.WarningBackground" Color="#10E5A000" />
    <SolidColorBrush x:Key="GameDetail.Status.WarningBorder" Color="{DynamicResource App.Warning}" />

    <!-- استایل متون هیرو کارت -->
    <Style x:Key="GameHeroTitleStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource PeydaBold}"/>
        <Setter Property="FontSize" Value="32"/>
        <Setter Property="Foreground" Value="{DynamicResource GameDetail.TextPrimary}"/>
        <Setter Property="HorizontalAlignment" Value="Right"/>
    </Style>

    <Style x:Key="GameHeroGenreStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource PeydaLight}"/>
        <Setter Property="FontSize" Value="17"/>
        <Setter Property="Foreground" Value="{DynamicResource GameDetail.TextSecondary}"/>
        <Setter Property="Margin" Value="0,2,0,0"/>
        <Setter Property="HorizontalAlignment" Value="Right"/>
    </Style>

    <Style x:Key="GameHeroDescriptionStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource PeydaLight}"/>
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="Foreground" Value="{DynamicResource GameDetail.TextPrimary}"/>
        <Setter Property="TextWrapping" Value="Wrap"/>
        <Setter Property="LineHeight" Value="28"/>
        <Setter Property="FlowDirection" Value="RightToLeft"/>
        <Setter Property="TextAlignment" Value="Right"/>
        <Setter Property="HorizontalAlignment" Value="Stretch"/>
        <Setter Property="Margin" Value="0,5,0,15"/>
    </Style>

    <!-- استایل کارت متادیتا (GameInfoCard) -->
    <Style TargetType="{x:Type c:GameInfoCard}">
        <Setter Property="Background" Value="{DynamicResource GameDetail.CardBackground}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource GameDetail.Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="30"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type c:GameInfoCard}">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="{TemplateBinding CornerRadius}"
                            Padding="{TemplateBinding Padding}">
                        <Border.Effect>
                            <DropShadowEffect Color="#000000" BlurRadius="25" ShadowDepth="4" Opacity="0.5"/>
                        </Border.Effect>
                        <ContentPresenter />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- استایل دکمه‌های اصلی و درخشش زرد زیبای آن‌ها -->
    <Style x:Key="PrimaryActionButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="{DynamicResource GameDetail.Highlight}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Height" Value="50"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="ButtonBorder"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="12">
                        <Border.Effect>
                            <DropShadowEffect x:Name="GlowEffect" Color="{DynamicResource GameDetail.Highlight.Color}" BlurRadius="10" ShadowDepth="0" Opacity="0"/>
                        </Border.Effect>
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="ButtonBorder" Property="Background" Value="#1Affff3d"/>
                            <Setter TargetName="ButtonBorder" Property="BorderBrush" Value="#80ffff3d"/>
                            <Setter TargetName="ButtonBorder" Property="BorderThickness" Value="1.5"/>
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="GlowEffect"
                                                         Storyboard.TargetProperty="Opacity"
                                                         To="0.6" Duration="0:0:0.2"/>
                                        <DoubleAnimation Storyboard.TargetName="GlowEffect"
                                                         Storyboard.TargetProperty="BlurRadius"
                                                         To="15" Duration="0:0:0.2"/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="GlowEffect"
                                                         Storyboard.TargetProperty="Opacity"
                                                         To="0.0" Duration="0:0:0.2"/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="ButtonBorder" Property="Background" Value="#33ffff3d"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- استایل تگ‌های کپسولی (Badge) کاملاً کپسوله‌شده بدون تداخل اولویت WPF -->
    <Style x:Key="CategoryBadgeStyle" TargetType="{x:Type c:GameBadge}">
        <Setter Property="Background" Value="{DynamicResource GameDetail.BadgeBackground}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource GameDetail.Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="14,6"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type c:GameBadge}">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="12"
                            Padding="{TemplateBinding Padding}">
                        <StackPanel Orientation="Horizontal" VerticalAlignment="Center" HorizontalAlignment="Center">
                            <!-- نقطه رنگی استاتوس -->
                            <Ellipse x:Name="StatusDot" Width="8" Height="8" Margin="0,0,10,0" VerticalAlignment="Center"
                                     Fill="{TemplateBinding DotBrush}"
                                     Visibility="{Binding HasDot, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource BooleanToVisibilityConverter}, FallbackValue=Collapsed}"/>
                            <!-- برچسب (مثلاً سازنده:) -->
                            <TextBlock x:Name="LabelText" Text="{TemplateBinding Label}"
                                       FontFamily="{StaticResource PeydaMedium}" FontSize="13"
                                       Foreground="{DynamicResource GameDetail.TextSecondary}" VerticalAlignment="Center" Margin="0,0,4,0"
                                       Visibility="{Binding HasLabel, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource BooleanToVisibilityConverter}, FallbackValue=Collapsed}"/>
                            <!-- مقدار متنی -->
                            <TextBlock x:Name="ValueText" Text="{TemplateBinding Text}"
                                       FontFamily="{StaticResource PeydaBold}" FontSize="13"
                                       Foreground="{TemplateBinding TextForeground}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="GenreBadgeStyle" TargetType="{x:Type c:GameBadge}" BasedOn="{StaticResource CategoryBadgeStyle}">
    </Style>

    <Style TargetType="{x:Type c:GameBadge}" BasedOn="{StaticResource CategoryBadgeStyle}">
    </Style>

    <Style x:Key="StatusBadgeStyle" TargetType="Border">
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Background" Value="{DynamicResource GameDetail.Status.SuccessBackground}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource GameDetail.Status.SuccessBorder}"/>
    </Style>
</ResourceDictionary>
```

---

### بخش ۲: بازسازی ساختار گرید و چیدمان دو ردیفه/متقارن
برای رفع مشکل به هم ریختگی و ستون‌های عمودی، گرید اصلی پنجره را به صورت دو ردیفه (ردیف بالا: بنر هیرو + اطلاعات نشست، ردیف پایین: اطلاعات تفصیلی و لوگوی بازی + پنل سخت‌افزاری) مپ کنید.

**مسیر فایل:** `Sayra.UI/Views/GameDetailWindow.xaml`

در کدهای خود، گرید بعد از `<c:TopBar ... />` را به صورت زیر بازنویسی کنید:

```xml
            <!-- CONTENT BODY (تقارن ۱۰۰٪ به صورت متقارن و ستونی) -->
            <Grid Grid.Row="1" FlowDirection="LeftToRight" Margin="75,30,75,60">
                <Grid.RowDefinitions>
                    <!-- ردیف دکمه بازگشت و نان‌ریزه -->
                    <RowDefinition Height="Auto" />
                    <!-- فاصله‌گذار -->
                    <RowDefinition Height="35" />
                    <!-- بدنه اصلی متقارن -->
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <!-- هدر: دکمه بازگشت و نان‌ریزه -->
                <Button Grid.Row="0" Style="{DynamicResource GlassDetailButtonStyle}" Click="Back_Click" HorizontalAlignment="Left" Width="160">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <Path Data="M10 19l-7-7m0 0l7-7m-7 7h18" Stroke="{DynamicResource TextPrimaryBrush}" StrokeThickness="2" Width="16" Height="16" Stretch="Uniform" Margin="0,0,10,0"/>
                        <TextBlock Text="بازگشت" FontFamily="{StaticResource PeydaMedium}" FontSize="15" Foreground="{DynamicResource TextPrimaryBrush}"/>
                    </StackPanel>
                </Button>

                <StackPanel Grid.Row="0" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center" FlowDirection="RightToLeft">
                    <TextBlock Text="داشبورد / بازی ها / " FontFamily="{StaticResource PeydaMedium}" FontSize="16" Foreground="{DynamicResource TextSecondaryBrush}"/>
                    <TextBlock x:Name="BreadcrumbGameTitle" Text="ولورانت" FontFamily="{StaticResource PeydaBold}" FontSize="16" Foreground="{DynamicResource GameDetail.Highlight}"/>
                </StackPanel>

                <!-- گرید چیدمان اصلی متقارن (کارت‌ها و پنل‌ها به صورت متقارن روی یک ردیف و ستون چیده می‌شوند) -->
                <Grid Grid.Row="2" VerticalAlignment="Stretch">
                    <Grid.ColumnDefinitions>
                        <!-- ستون چپ عریض برای هیرو بنر و جزئیات بازی -->
                        <ColumnDefinition Width="Auto" />
                        <!-- ستون میانی فاصله‌گذار داینامیک -->
                        <ColumnDefinition Width="*" />
                        <!-- ستون راست برای اطلاعات نشست و مشخصات سخت‌افزاری سیستم -->
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>

                    <Grid.RowDefinitions>
                        <!-- ردیف بالا: هیرو ایمیج و اطلاعات نشست سیستم -->
                        <RowDefinition Height="Auto" />
                        <!-- فاصله‌گذار ردیفی متقارن بدون استفاده از مارجین هک شده -->
                        <RowDefinition Height="20" />
                        <!-- ردیف پایین: اطلاعات تفصیلی بازی به همراه لوگو و پنل سخت‌افزاری سیستم -->
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>

                    <!-- ========================================== -->
                    <!-- بخش بالایی (TOP SECTION) -->
                    <!-- ========================================== -->

                    <!-- ستون چپ: کارت هیرو ایمیج / بنر بزرگ بازی -->
                    <c:GameInfoCard Grid.Row="0" Grid.Column="0" Width="1296" Height="406" CornerRadius="20" Padding="0"
                                    Background="{DynamicResource GameDetail.CardBackground}"
                                    BorderBrush="{DynamicResource GameDetail.Border}"
                                    BorderThickness="1">
                        <Grid ClipToBounds="True">
                            <Grid Background="{DynamicResource GameDetail.CoverPlaceholderBackground}">
                                <!-- آیکون پیش‌فرض دسته بازی در صورت عدم وجود پوستر -->
                                <Path Data="M21,6H3C1.9,6 1,6.9 1,8V16C1,17.1 1.9,18 3,18H21C22.1,18 23,17.1 23,16V8C23,6.9 22.1,6 21,6M6,15H4V13H2V11H4V9H6V11H8V13H6V15M15,11A1.5,1.5 0 1,1 16.5,9.5A1.5,1.5 0 0,1 15,11M19,14A1.5,1.5 0 1,1 20.5,12.5A1.5,1.5 0 0,1 19,14Z"
                                      Fill="{DynamicResource GameDetail.GamepadIconFill}"
                                      Width="50" Height="50"
                                      Stretch="Uniform"
                                      HorizontalAlignment="Center"
                                      VerticalAlignment="Center" />
                                <Image x:Name="DetailCoverImage" Stretch="UniformToFill" ImageFailed="DetailCoverImage_ImageFailed"/>
                            </Grid>
                        </Grid>
                    </c:GameInfoCard>

                    <!-- ستون راست: کارت اطلاعات نشست سیستم (هم‌تراز با ردیف هیرو بنر بازی) -->
                    <c:GameInfoCard Grid.Row="0" Grid.Column="2" Width="454" Height="406" CornerRadius="20" Padding="25"
                                    Background="{DynamicResource GameDetail.CardBackground}"
                                    BorderBrush="{DynamicResource GameDetail.Border}"
                                    BorderThickness="1">
                        <c:GameInfoCard.DataContext>
                            <vm:SessionHeroViewModel />
                        </c:GameInfoCard.DataContext>
                        <c:GameInfoCard.Effect>
                            <DropShadowEffect Color="{DynamicResource App.Accent}" BlurRadius="12" ShadowDepth="0" Opacity="0.1" />
                        </c:GameInfoCard.Effect>

                        <Grid FlowDirection="RightToLeft">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                                <RowDefinition Height="Auto" />
                            </Grid.RowDefinitions>

                            <Grid Grid.Row="0" Margin="0,0,0,12">
                                <TextBlock Text="PC-08" FontFamily="{StaticResource PeydaBold}" FontSize="24" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Left" VerticalAlignment="Center" />
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                                    <Path Data="M12 6v6h4.5M21 12a9 9 0 11-18 0 9 9 0 0118 0z" Stroke="{DynamicResource GameDetail.Highlight}" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Fill="Transparent" Width="22" Height="22" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                    <TextBlock Text="session information" FontFamily="{StaticResource PeydaBold}" FontSize="20" Foreground="{DynamicResource GameDetail.Highlight}" VerticalAlignment="Center" />
                                </StackPanel>
                            </Grid>

                            <StackPanel Grid.Row="1" HorizontalAlignment="Right" Margin="0,0,0,15">
                                <TextBlock Text="زمان استفاده شده" FontFamily="{StaticResource PeydaMedium}" FontSize="18" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Right" />
                                <TextBlock Text="{Binding SessionTime}" FontFamily="{StaticResource BlackOpsOne}" FontSize="54" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Right" Margin="0,4,0,0">
                                    <TextBlock.Effect>
                                        <DropShadowEffect Color="{DynamicResource App.Primary}" BlurRadius="15" ShadowDepth="0" Opacity="0.4" />
                                    </TextBlock.Effect>
                                </TextBlock>
                            </StackPanel>

                            <Border Grid.Row="2" Height="1.5" Background="{DynamicResource GameDetail.Highlight}" Margin="0,0,0,15" />

                            <Grid Grid.Row="3" Margin="0,0,0,15">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>

                                <Grid Grid.Row="0" Margin="0,0,0,10">
                                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                                        <Path Data="{DynamicResource HourlyRateIconGeometry}" Stroke="{DynamicResource GameDetail.Highlight}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Fill="Transparent" Width="18" Height="18" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0">
                                            <Path.Effect>
                                                <DropShadowEffect Color="{DynamicResource App.Primary}" BlurRadius="6" ShadowDepth="0" Opacity="0.3"/>
                                            </Path.Effect>
                                        </Path>
                                        <TextBlock Text="نرخ ساعتی" FontFamily="{StaticResource PeydaMedium}" FontSize="19" Foreground="{DynamicResource GameDetail.Highlight}" VerticalAlignment="Center" />
                                    </StackPanel>
                                    <TextBlock Text="{Binding HourlyRate}" FontFamily="{StaticResource PeydaBold}" FontSize="20" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Right" VerticalAlignment="Center" />
                                </Grid>

                                <Grid Grid.Row="1" Margin="0,0,0,10">
                                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                                        <Path Data="{DynamicResource CurrentCostIconGeometry}" Stroke="{DynamicResource GameDetail.Highlight}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Fill="Transparent" Width="18" Height="18" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0">
                                            <Path.Effect>
                                                <DropShadowEffect Color="{DynamicResource App.Primary}" BlurRadius="6" ShadowDepth="0" Opacity="0.3"/>
                                            </Path.Effect>
                                        </Path>
                                        <TextBlock Text="هزینه تا کنون" FontFamily="{StaticResource PeydaMedium}" FontSize="19" Foreground="{DynamicResource GameDetail.Highlight}" VerticalAlignment="Center" />
                                    </StackPanel>
                                    <TextBlock Text="{Binding CurrentCost}" FontFamily="{StaticResource PeydaBold}" FontSize="20" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Right" VerticalAlignment="Center" />
                                </Grid>

                                <Grid Grid.Row="2" Margin="0,0,0,10">
                                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                                        <Path Data="{DynamicResource StartTimeIconGeometry}" Stroke="{DynamicResource GameDetail.Highlight}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Fill="Transparent" Width="18" Height="18" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0">
                                            <Path.Effect>
                                                <DropShadowEffect Color="{DynamicResource App.Primary}" BlurRadius="6" ShadowDepth="0" Opacity="0.3"/>
                                            </Path.Effect>
                                        </Path>
                                        <TextBlock Text="زمان شروع" FontFamily="{StaticResource PeydaMedium}" FontSize="19" Foreground="{DynamicResource GameDetail.Highlight}" VerticalAlignment="Center" />
                                    </StackPanel>
                                    <TextBlock Text="{Binding StartTime}" FontFamily="{StaticResource PeydaBold}" FontSize="20" Foreground="{DynamicResource GameDetail.Highlight}" HorizontalAlignment="Right" VerticalAlignment="Center" />
                                </Grid>
                            </Grid>

                            <Button Grid.Row="4" Style="{DynamicResource EndSessionButtonStyle}" Click="EndSession_Click" Width="240" Height="48" HorizontalAlignment="Center" VerticalAlignment="Bottom"/>
                        </Grid>
                    </c:GameInfoCard>

                    <!-- ========================================== -->
                    <!-- بخش پایینی (BOTTOM SECTION) -->
                    <!-- ========================================== -->

                    <!-- ستون چپ: کارت اطلاعات تفصیلی بازی به همراه لوگو و مشخصات متنی -->
                    <c:GameInfoCard Grid.Row="2" Grid.Column="0" Width="1296" CornerRadius="20" Padding="30" VerticalAlignment="Stretch"
                                    Background="{DynamicResource GameDetail.CardBackground}"
                                    BorderBrush="{DynamicResource GameDetail.Border}"
                                    BorderThickness="1">
                        <Grid FlowDirection="RightToLeft">
                            <Grid.ColumnDefinitions>
                                <!-- بخش راست کارت (RTL Col 0): متون و دکمه شروع بازی -->
                                <ColumnDefinition Width="*" />
                                <!-- فاصله‌گذار ستونی -->
                                <ColumnDefinition Width="30" />
                                <!-- بخش چپ کارت (RTL Col 2): لوگوی تفصیلی بازی -->
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>

                            <!-- چپ: لوگوی بازی (بازیابی لوگوی حذف شده) -->
                            <Border Grid.Column="2" VerticalAlignment="Center" Width="240" Height="240" Background="{DynamicResource GameDetail.BadgeBackground}" CornerRadius="16" BorderBrush="{DynamicResource GameDetail.Border}" BorderThickness="1" ClipToBounds="True">
                                <Image x:Name="DetailLogoImage" Stretch="Uniform" Margin="10" RenderOptions.BitmapScalingMode="HighQuality">
                                    <Image.Effect>
                                        <DropShadowEffect Color="Black" BlurRadius="5" ShadowDepth="1" Opacity="0.5"/>
                                    </Image.Effect>
                                </Image>
                            </Border>

                            <!-- راست: مشخصات متنی بازی (شامل عنوان، توضیحات، سازنده و لانچر) -->
                            <Grid Grid.Column="0" VerticalAlignment="Stretch">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" /> <!-- عنوان و ژانر بازی -->
                                    <RowDefinition Height="*" />    <!-- توضیحات بازی -->
                                    <RowDefinition Height="Auto" /> <!-- مدال‌های متادیتا -->
                                    <RowDefinition Height="Auto" /> <!-- وضعیت آماده به بازی / در حال بازی -->
                                </Grid.RowDefinitions>

                                <!-- عنوان و ژانر -->
                                <StackPanel Grid.Row="0" VerticalAlignment="Center" HorizontalAlignment="Right" Margin="0,0,0,10">
                                    <TextBlock x:Name="DetailTitle" Style="{DynamicResource GameHeroTitleStyle}" />
                                    <TextBlock x:Name="DetailGenre" Style="{DynamicResource GameHeroGenreStyle}" />
                                </StackPanel>

                                <!-- توضیحات بازی -->
                                <TextBlock x:Name="DetailDescription" Grid.Row="1" Style="{DynamicResource GameHeroDescriptionStyle}" />

                                <!-- متادیتاها با استایل سنترالیزه جدید و اولویت تصحیح‌شده WPF -->
                                <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" FlowDirection="RightToLeft" Margin="0,0,0,20">
                                    <c:GameBadge x:Name="DeveloperBadge" Style="{DynamicResource CategoryBadgeStyle}" HasLabel="True" Label="سازنده: " Margin="0,0,10,0"/>
                                    <c:GameBadge x:Name="ReleaseYearBadge" Style="{DynamicResource CategoryBadgeStyle}" HasLabel="True" Label="سال انتشار: " Margin="0,0,10,0"/>
                                    <c:GameBadge x:Name="LauncherBadge" Style="{DynamicResource GenreBadgeStyle}" HasLabel="True" Label="لانچر: " TextForeground="{DynamicResource GameDetail.Highlight}"/>
                                </StackPanel>

                                <!-- دکمه بزرگ آماده به بازی / درحال بازی -->
                                <Button Grid.Row="3" x:Name="PlayButton" Click="PlayGame_Click" Style="{DynamicResource PrimaryActionButtonStyle}" Background="Transparent" BorderBrush="Transparent" BorderThickness="0" HorizontalAlignment="Right" Padding="0" Cursor="Hand" Margin="0,0,0,10">
                                    <Button.Template>
                                        <ControlTemplate TargetType="Button">
                                            <ContentPresenter />
                                        </ControlTemplate>
                                    </Button.Template>
                                    <Border x:Name="StatusBadgeBorder" Width="288" Height="50" Style="{DynamicResource StatusBadgeStyle}">
                                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                                            <Ellipse x:Name="StatusBadgeDot" Width="8" Height="8" Fill="{DynamicResource GameDetail.Status.SuccessBorder}" Margin="0,0,10,0" VerticalAlignment="Center"/>
                                            <TextBlock x:Name="DetailStatus" Text="وضعیت" FontFamily="{StaticResource PeydaMedium}" FontSize="15" Foreground="{DynamicResource GameDetail.Status.SuccessBorder}" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Border>
                                </Button>
                            </Grid>
                        </Grid>
                    </c:GameInfoCard>

                    <!-- ستون راست: پنل اطلاعات تفصیلی سخت‌افزار سیستم (هم‌تراز با ردیف پایینی) -->
                    <StackPanel Grid.Row="2" Grid.Column="2" VerticalAlignment="Top" Width="454">
                        <c:HardwarePanel x:Name="Hardware" Width="454" HorizontalAlignment="Stretch" VerticalAlignment="Top" />

                        <!-- لوگوی سایرا در زیر کل بخش چیدمان متقارن -->
                        <svgc:SvgViewbox Source="/Assets/sayra.svg" Width="Auto" Height="Auto" Opacity="0.2" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Margin="0,20,0,0" IsHitTestVisible="False" />
                    </StackPanel>
                </Grid>
            </Grid>
```

---

### بخش ۳: همگام‌سازی منطق کدپشت
چون تمام کنترل‌ها (شامل پس‌زمینه کلی پنجره، هدرها، کارت مشخصات، تگ‌ها و وضعیت) در گرید اصلی توزیع شده‌اند، کدپشت بسیار سبک و همگام با معماری جدید است:

**مسیر فایل:** `Sayra.UI/Views/GameDetailWindow.xaml.cs`

کدهای این کلاس را با دستورالعمل زیر مطابقت دهید:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sayra.UI.Models;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class GameDetailWindow : Window
    {
        private readonly GameItem _game;
        private readonly HomeWindow _dashboard;

        public GameDetailWindow(GameItem game, HomeWindow dashboard)
        {
            InitializeComponent();
            _game = game;
            _dashboard = dashboard;

            PopulateDetails();
        }

        private void PopulateDetails()
        {
            if (_game == null) return;

            // مقداردهی مستقیم به کنترل‌های والد در پنجره
            DetailTitle.Text = _game.Title;
            DetailGenre.Text = _game.Genre;
            BreadcrumbGameTitle.Text = _game.Title;
            DetailDescription.Text = string.IsNullOrEmpty(_game.Description)
                ? "توضیحاتی برای این بازی ثبت نشده است."
                : _game.Description;

            // مپ کردن تگ‌های کپسولی اختصاصی
            DeveloperBadge.Text = string.IsNullOrEmpty(_game.Developer) ? "نا مشخص" : _game.Developer;
            ReleaseYearBadge.Text = string.IsNullOrEmpty(_game.ReleaseYear) ? "نا مشخص" : _game.ReleaseYear;
            LauncherBadge.Text = string.IsNullOrEmpty(_game.Launcher) ? "Custom" : _game.Launcher;

            // تنظیم وضعیت و استایل داینامیک آن با تم سنترال
            string status = _game.Status;
            DetailStatus.Text = status;

            if (!string.IsNullOrEmpty(status))
            {
                string upperStatus = status.ToUpperInvariant();
                if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.SuccessBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    DetailStatus.Text = "در حال بازی";
                }
                else if (upperStatus == "LOCKED" || upperStatus == "UNAVAILABLE")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.DangerBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    DetailStatus.Text = upperStatus == "LOCKED" ? "قفل شده" : "غیر فعال";
                }
                else
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.WarningBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    DetailStatus.Text = "آماده بازی";
                }
            }

            // لود آرت‌ورک، لوگو و بک‌دراپ اتمسفریک کلی پنجره
            UpdateCoverImage();
            UpdateLogoImage();
            UpdateBackgroundImage();
        }

        private void UpdateCoverImage()
        {
            if (DetailCoverImage == null) return;
            string path = _game.ImagePath;
            if (string.IsNullOrEmpty(path)) { DetailCoverImage.Source = null; return; }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (path.StartsWith("pack://") || path.Contains("://")) bitmap.UriSource = new Uri(path);
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    bitmap.UriSource = !System.IO.File.Exists(fullPath) ? new Uri(path, UriKind.RelativeOrAbsolute) : new Uri(fullPath, UriKind.Absolute);
                }
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();
                DetailCoverImage.Source = bitmap;
            }
            catch { DetailCoverImage.Source = null; }
        }

        private void UpdateLogoImage()
        {
            if (DetailLogoImage == null) return;
            string path = _game.LogoImage;
            if (string.IsNullOrEmpty(path)) { DetailLogoImage.Source = null; return; }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (path.StartsWith("pack://") || path.Contains("://")) bitmap.UriSource = new Uri(path);
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    bitmap.UriSource = !System.IO.File.Exists(fullPath) ? new Uri(path, UriKind.RelativeOrAbsolute) : new Uri(fullPath, UriKind.Absolute);
                }
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();
                DetailLogoImage.Source = bitmap;
            }
            catch { DetailLogoImage.Source = null; }
        }

        private void UpdateBackgroundImage()
        {
            if (DetailBackgroundImage == null) return;
            string path = _game.BackgroundImage;
            if (string.IsNullOrEmpty(path)) { DetailBackgroundImage.Source = null; return; }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (path.StartsWith("pack://") || path.Contains("://")) bitmap.UriSource = new Uri(path);
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    bitmap.UriSource = !System.IO.File.Exists(fullPath) ? new Uri(path, UriKind.RelativeOrAbsolute) : new Uri(fullPath, UriKind.Absolute);
                }
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();
                DetailBackgroundImage.Source = bitmap;
            }
            catch { DetailBackgroundImage.Source = null; }
        }

        private void DetailCoverImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try { if (sender is Image img) img.Visibility = Visibility.Collapsed; } catch { }
        }

        private void Back_Click(object sender, RoutedEventArgs e) => this.Close();

        private async void EndSession_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که می‌خواهید خارج شوید؟",
                "سیستم سایرا",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            );

            if (result == MessageBoxResult.Yes)
            {
                Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال خروج از سیستم...");
                await System.Threading.Tasks.Task.Delay(1000);
                Application.Current.Shutdown();
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dashboard.GameLib?.DataContext is GameLibraryViewModel vm)
                {
                    if (vm.PlayGameCommand != null && vm.PlayGameCommand.CanExecute(_game))
                    {
                        vm.PlayGameCommand.Execute(_game);
                    }
                }
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Failed to launch game: {ex.Message}");
            }
        }
    }
}
```
