namespace ShiCi {
public static class VisualTheme {
    public const string UiFont="Segoe UI, Microsoft YaHei UI";
    public const string WordFont="Georgia";
    public const string NumberFont="Cascadia Mono, Consolas";
    static string ipaFont;
    public static string PhoneticFont { get {
        if(ipaFont!=null)return ipaFont;
        string folder=System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"fonts");System.IO.Directory.CreateDirectory(folder);
        string file=System.IO.Path.Combine(folder,"NotoSans-Regular.ttf");
        if(!System.IO.File.Exists(file))using(var input=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("VocXuan.IPA.ttf"))using(var output=System.IO.File.Create(file))input.CopyTo(output);
        return ipaFont=new System.Uri(folder+System.IO.Path.DirectorySeparatorChar).AbsoluteUri+"#Noto Sans";
    } }
    public const string Xaml=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='TextBlock'><Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/></Style>
<Style TargetType='ToolTip'><Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/><Setter Property='FontSize' Value='12'/><Setter Property='Padding' Value='9,6'/></Style>
<Style TargetType='Button'>
 <Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/><Setter Property='FontSize' Value='14'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='18,10'/><Setter Property='Foreground' Value='#405247'/><Setter Property='Background' Value='#FFFFFF'/><Setter Property='BorderBrush' Value='#E1E6DF'/><Setter Property='BorderThickness' Value='1'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
  <Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
   <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
  </Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#AEC2B3'/><Setter TargetName='box' Property='Opacity' Value='0.94'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='box' Property='Opacity' Value='0.80'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#467A60'/><Setter TargetName='box' Property='Effect'><Setter.Value><DropShadowEffect Color='#83AC91' BlurRadius='5' ShadowDepth='0' Opacity='0.24'/></Setter.Value></Setter></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='0.32'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='ChoiceToggle' TargetType='ToggleButton'>
 <Setter Property='Focusable' Value='False'/><Setter Property='ClickMode' Value='Press'/><Setter Property='Cursor' Value='Hand'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToggleButton'>
  <Border x:Name='choice' Background='#FFFFFF' BorderBrush='#DEE5DC' BorderThickness='1' CornerRadius='9'>
   <Path Data='M 0,0 L 4,4 L 8,0' Stroke='#7D8B7F' StrokeThickness='1.3' StrokeStartLineCap='Round' StrokeEndLineCap='Round' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,16,0'/>
  </Border>
  <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='choice' Property='BorderBrush' Value='#AFC2B1'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='choice' Property='BorderBrush' Value='#6F9478'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ComboBox'>
 <Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/><Setter Property='FontSize' Value='14'/><Setter Property='MinHeight' Value='47'/><Setter Property='Foreground' Value='#35463B'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
  <Grid SnapsToDevicePixels='True'>
   <ToggleButton Style='{StaticResource ChoiceToggle}' IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'/>
   <ContentPresenter x:Name='selected' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}' Margin='15,0,36,0' HorizontalAlignment='Left' VerticalAlignment='Center'/>
   <TextBox x:Name='PART_EditableTextBox' FontFamily='{TemplateBinding FontFamily}' FontSize='{TemplateBinding FontSize}' Foreground='{TemplateBinding Foreground}' Background='Transparent' BorderThickness='0' Margin='13,0,34,0' Padding='0' VerticalAlignment='Center' Visibility='Hidden' Text='{Binding Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, RelativeSource={RelativeSource TemplatedParent}}' IsReadOnly='{TemplateBinding IsReadOnly}'/>
   <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False' IsOpen='{TemplateBinding IsDropDownOpen}' PopupAnimation='Fade'>
    <Border Background='White' BorderBrush='#E2E8DE' BorderThickness='1' CornerRadius='10' Padding='5' Margin='0,5,0,8' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
     <Border.Effect><DropShadowEffect Color='#233D2C' BlurRadius='10' ShadowDepth='2' Opacity='0.08'/></Border.Effect>
     <ScrollViewer MaxHeight='310' CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
    </Border>
   </Popup>
  </Grid>
  <ControlTemplate.Triggers><Trigger Property='IsEditable' Value='True'><Setter TargetName='selected' Property='Visibility' Value='Hidden'/><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ComboBoxItem'>
 <Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/><Setter Property='FontSize' Value='14'/><Setter Property='Foreground' Value='#35463B'/><Setter Property='Padding' Value='11,9'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' Background='Transparent' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='#F0F4ED'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='#E8F0E5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='TextBox'><Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/></Style>
<Style TargetType='CheckBox'>
 <Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#7B877C'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'>
  <StackPanel Orientation='Horizontal'><Border x:Name='outline' Width='16' Height='16' CornerRadius='4' Background='White' BorderBrush='#CAD6C7' BorderThickness='1' VerticalAlignment='Center'><Path x:Name='tick' Data='M 3,7 L 6,10 L 12,3' Stroke='#2B6B50' StrokeThickness='1.6' StrokeLineJoin='Round' StrokeStartLineCap='Round' StrokeEndLineCap='Round' Visibility='Collapsed'/></Border><ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center'/></StackPanel>
  <ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='outline' Property='BorderBrush' Value='#7B9A7F'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='outline' Property='BorderBrush' Value='#376F51'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='ScrollThumb' TargetType='Thumb'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'><Border x:Name='thumb' Background='#CDD6C9' CornerRadius='3' Margin='3,2'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='thumb' Property='Background' Value='#A5B6A2'/></Trigger><Trigger Property='IsDragging' Value='True'><Setter TargetName='thumb' Property='Background' Value='#8CA78C'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='11'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb Style='{StaticResource ScrollThumb}'/></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
}
}
