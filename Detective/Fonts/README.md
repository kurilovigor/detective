This folder is intended to contain font files used by the application.

Please place the "Segoe Fluent Icons.ttf" file here with the exact filename:

	Fonts/Segoe Fluent Icons.ttf

Do not commit the font file to source control unless you have the rights to redistribute it. If you prefer to keep fonts out of source control, put them on your machine and add them to the project (see Detective.csproj) or configure your CI to provide them.

Once the file is placed, the project already includes it as a WPF Resource so the following pack URI in App.xaml will work:

	pack://application:,,,/;component/Fonts/#Segoe Fluent Icons

If you want me to add the actual .ttf file from a local path, provide the file or confirm redistribution rights.