# Anime Downloader

A powerful WPF desktop application for downloading anime episodes from AnimeHeaven with intelligent multi-threaded downloading, priority management, and smart caching.

## ✨ Features

### Core Functionality
- **Automated Episode Scraping** - Uses Selenium WebDriver to automatically discover and extract episode download links
- **Smart Caching System** - Caches scraped episode links for 24 hours to minimize redundant scraping operations
- **Multithreaded Downloads** - Concurrent episode downloads with configurable worker threads for optimal performance
- **Priority Queue Management** - Organize downloads with High/Medium/Low priorities and dynamic priority updates
- **Resume Support** - Automatically resumes interrupted downloads from the exact point of interruption

### Task Management
- View all active and completed download tasks in real-time
- Monitor download progress with live status updates
- Access detailed logs for each task and individual episode
- Cancel tasks or pause/resume specific downloads
- Double-click tasks to view per-episode download status and detailed progress

### Additional Features
- Persistent logging system with export functionality
- Clean, intuitive WPF interface
- Automatic retry logic for failed downloads
- Episode validation and duplicate detection

## 📋 System Requirements

- **Operating System**: Windows 10/11
- **.NET Runtime**: .NET 8.0 or higher
- **Browser**: Google Chrome (required for Selenium web scraping)
- **Internet**: Active internet connection
- **Storage**: Sufficient disk space for downloaded episodes

## 🚀 Installation

1. Download the latest release from the releases page
2. Extract the archive to your preferred location
3. Verify the `Drivers` folder contains `chromedriver.exe` (bundled with release)
4. Launch `AnimeBingeDownloader.exe`

> **Note**: If you encounter issues with ChromeDriver, ensure your Chrome browser version matches the driver version.

## 📖 Usage

### Adding a Download Task

1. Navigate to the **New Task** tab
2. Enter the AnimeHeaven URL (format: `https://animeheaven.me/watch/anime-title`)
3. Select a download directory or use the default location
4. Click **Add to Queue & Start Task**

The application will automatically begin scraping episode links and start downloading.

### Managing Tasks

Switch to the **Task Manager** tab to:

- **Monitor Progress** - View real-time download status, episode counts, and elapsed time
- **Manage Tasks** - Right-click any task for options:
  - View detailed logs
  - Cancel ongoing tasks
  - Set priority (High/Medium/Low)
  - Pause or resume downloads
- **View Episode Details** - Double-click a task to see individual episode download status

### Viewing and Exporting Logs

1. Select a task in the Task Manager
2. Click **View Selected Task Log** or navigate to the **Task Log** tab
3. Use the **Export Log** button to save logs for troubleshooting or sharing

## 🏗️ Architecture

### Core Components

| Component | Purpose |
|-----------|---------|
| **TaskViewModel** | Manages individual download tasks with status tracking and logging |
| **EpisodeTask** | Represents individual episode downloads with resume capability |
| **TaskCoordinator** | Orchestrates scraping and download operations |
| **ScrapingService** | Selenium-based web scraper for episode link extraction |
| **DownloadService** | Multi-threaded download manager with priority queue |
| **CacheService** | JSON-based caching system for scraped episode data |
| **IndexedPriorityQueue** | Custom priority queue with O(log N) priority updates |

### Download Workflow

1. **Scraping Phase** - Selenium navigates the anime page and extracts episode links
2. **Cache Validation** - Checks for valid cached data (less than 24 hours old)
3. **Queue Population** - Episodes are added to the priority queue based on settings
4. **Concurrent Processing** - Configurable worker threads process downloads in parallel
5. **Resume Handling** - Partial downloads resume automatically using HTTP Range headers
6. **Status Updates** - Real-time progress tracking with comprehensive logging

## ⚙️ Configuration

### Download Settings

Modify worker thread count in `Services/DownloadService.cs`:

```csharp
Configuration.MaxWorkers  // Number of concurrent downloads (default: auto-configured)
```

### Cache Settings

Adjust cache expiration in `Services/CacheService.cs`:

```csharp
private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);
```

## 📁 Project Structure

```
AnimeBingeDownloader/
├── Models/
│   ├── TaskViewModel.cs          # Task state and lifecycle management
│   ├── EpisodeTask.cs             # Episode download logic and resume support
│   ├── IndexedPriorityQueue.cs    # Optimized priority queue implementation
│   └── enums.cs                   # Status and priority enumerations
├── Services/
│   ├── TaskCoordinator.cs         # High-level task orchestration
│   ├── ScrapingService.cs         # Selenium-based web scraping engine
│   ├── DownloadService.cs         # Multi-threaded download manager
│   ├── CacheService.cs            # Episode link caching system
│   └── AppStorageService.cs       # Local storage and settings management
├── Views/
│   ├── MainWindow.xaml            # Main application UI layout
│   └── MainWindow.xaml.cs         # UI event handlers and logic
└── App.xaml                       # Application-level resources
```

## 💾 Storage Locations

- **Cache File**: `%LocalAppData%\AnimeDownloader\anime_links_cache.json`
- **Downloaded Episodes**: `%UserProfile%\Downloads\AnimeHeaven\[Anime Title]\`
- **Logs**: Accessible via the Task Log viewer or export function

## ⚠️ Known Limitations

- Only supports AnimeHeaven website structure (other sites not compatible)
- Requires visible Chrome window during scraping (headless mode not supported)
- Special episodes (e.g., episode 12.5, OVAs) are automatically skipped
- Cache automatically expires after 24 hours
- No built-in subtitle download support

## 🔧 Troubleshooting

### Scraping Issues

**Problem**: Scraping fails or times out

**Solutions**:
- Verify Google Chrome is installed and up to date
- Ensure `chromedriver.exe` exists in the `Drivers` folder
- Check if AnimeHeaven website structure has changed
- Temporarily disable antivirus if it's blocking Selenium

### Download Errors

**Problem**: Downloads fail or won't start

**Solutions**:
- Verify stable internet connection
- Check available disk space in download directory
- Review task logs for specific error messages
- Try changing download priority or pausing/resuming

### Application Crashes

**Problem**: Application closes unexpectedly

**Solutions**:
- Export logs before closing the application
- Check Windows Event Viewer for .NET exceptions
- Verify .NET 8.0 runtime is properly installed
- Try running as administrator
- Ensure no conflicting Chrome instances are running

### ChromeDriver Version Mismatch

**Problem**: "ChromeDriver version mismatch" error

**Solutions**:
- Update Chrome browser to the latest version
- Replace `chromedriver.exe` with a compatible version
- Use WebDriverManager auto-update feature (if available)

## 📜 License

This project is provided for **educational purposes only**. Users are solely responsible for complying with AnimeHeaven's terms of service and copyright laws in their jurisdiction.

## 🙏 Credits

Built with:
- **WPF** (.NET 8.0) - User interface framework
- **Selenium WebDriver** - Web automation and scraping
- **WebDriverManager** - Automatic ChromeDriver management

## ⚖️ Legal Disclaimer

**IMPORTANT**: This tool is intended for personal use only. Downloading copyrighted content without permission may be illegal in your country. Users must:

- Respect content creators and their intellectual property rights
- Support official releases and streaming services
- Comply with all applicable laws and regulations
- Use this software at their own risk

The developers assume no liability for misuse of this software.