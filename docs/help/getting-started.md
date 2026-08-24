# Getting Started with Thiccdal

This guide walks you through installing, configuring, and launching Thiccdal on your stream PC for the first time. It covers both local development and container-based deployment.

## Prerequisites

### Required
- **Operating System**: Windows, macOS, or Linux
- **.NET 10 Runtime**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
  - Check your version: `dotnet --version`
- **Internet Connection**: Required for OAuth login to Twitch and other platforms
- **Modern Web Browser**: Chrome, Firefox, Safari, or Edge (for the control UI and OAuth flows)

### Optional (for advanced deployments)
- **Docker & Docker Compose**: For containerized deployments (see [Docker Deployment](#docker-deployment) section)
- **Git**: If you plan to clone and build from source

## Installation

### Option 1: Using Pre-Built Release (Recommended for Operators)

1. **Download the latest release**
   - Visit the [Releases page](https://github.com/ThindalTV/Thiccdal26/releases)
   - Download the `.zip` archive for your platform

2. **Extract the archive**
   ```bash
   # Windows
   Expand-Archive Thiccdal-v*.zip -DestinationPath C:\Streaming\Thiccdal

   # macOS/Linux
   unzip Thiccdal-v*.zip -d ~/streaming/thiccdal
   ```

3. **Navigate to the application folder**
   ```bash
   cd C:\Streaming\Thiccdal  # Windows
   cd ~/streaming/thiccdal   # macOS/Linux
   ```

### Option 2: Building from Source

1. **Clone the repository**
   ```bash
   git clone https://github.com/ThindalTV/Thiccdal26.git
   cd Thiccdal26
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run via Aspire (development)**
   ```bash
   dotnet run --project src/Aspire/Thiccdal.Aspire.AppHost
   ```

## Configuration

Thiccdal is configured through `appsettings.json`. All settings use the `IOptions<T>` pattern — no magic strings.

### Locating the Configuration File

- **Pre-built release**: `appsettings.json` is in the extracted folder
- **Source build**: `src/Thiccdal/appsettings.json`

### Basic Configuration Template

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=thiccdal.db"
  },
  "AllowedHosts": "*",
  "Twitch": {
    "ClientId": "",
    "ClientSecret": "",
    "RedirectUri": "https://localhost:7082/auth/twitch/callback",
    "OAuthBaseAddress": "https://id.twitch.tv/oauth2/",
    "Helix": {
      "BaseAddress": "https://api.twitch.tv/helix/",
      "StreamStateRefreshSeconds": 30,
      "SendChatMessagesViaHelix": true
    },
    "EventSub": {
      "WebSocketUrl": "wss://eventsub.wss.twitch.tv/ws",
      "ReconnectDelaySeconds": 5,
      "RequireModeratorAccess": true,
      "UseAnimatedEmotes": true
    },
    "Scopes": [
      "user:read:chat",
      "user:write:chat",
      "user:bot",
      "channel:bot",
      "moderator:read:followers",
      "channel:read:subscriptions",
      "bits:read",
      "channel:read:redemptions"
    ]
  }
}
```

### Logging Configuration

| Setting | Purpose | Values |
|---------|---------|--------|
| `LogLevel.Default` | Global log verbosity | `Debug`, `Information`, `Warning`, `Error` |
| `LogLevel."Microsoft.AspNetCore"` | ASP.NET Core framework logs | `Information`, `Warning`, `Error` |

**For production**: Set to `Information` for most components and `Warning` for ASP.NET Core.

### Database Configuration

**Connection String**: `ConnectionStrings.DefaultConnection`

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=thiccdal.db"
}
```

- **Location**: By default, `thiccdal.db` (SQLite) is created in the working directory where you run the app
- **Automatic Setup**: On first run, the database is created and migrations are applied automatically
- **Persistence**: The database persists between runs in the same directory

**To use a custom database path:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=/data/streaming/thiccdal.db"
}
```

## Running the Application

### Local Execution

**From a pre-built release:**
```bash
dotnet Thiccdal.dll
```

The app will:
1. Initialize the SQLite database (if needed)
2. Apply any pending migrations
3. Start the web server on a local port (typically `https://localhost:7082` or `http://localhost:5082`)
4. Display connection details in the console

**Open your browser** to the URL shown (e.g., `https://localhost:7082`) to access the control dashboard.

### Running in the Background (Windows)

To run Thiccdal as a background service on Windows:

```powershell
# Start as a background job
$process = Start-Process -FilePath "dotnet" -ArgumentList "Thiccdal.dll" -NoNewWindow -PassThru
Write-Host "Thiccdal started with PID: $($process.Id)"

# Stop the service
Stop-Process -Id $process.Id
```

### Troubleshooting Startup

| Issue | Solution |
|-------|----------|
| `"Port already in use"` | Another app is using the default port. Set `ASPNETCORE_URLS=http://localhost:5083` before running. |
| `"Database locked"` | Close any other instances of Thiccdal that have the database open. |
| `"Could not find dotnet"` | Install .NET 10 Runtime or use the full path to `dotnet.exe`. |
| `"Untrusted certificate warning"` | The HTTPS certificate is self-signed (expected in development). Click "Proceed anyway" or access via HTTP on port 5082. |

---

## Docker Deployment

⚠️ **Status**: Dockerfile and docker-compose.yml assets **do not yet exist** in the repository. This section documents the planned approach and how to prepare for containerized deployment.

### Prerequisites for Docker

- Docker Engine 20.10+ ([Install Docker](https://docs.docker.com/get-docker/))
- Docker Compose 1.29+ (usually included with Docker Desktop)
- At least 1 GB available disk space
- Access to write files in a volume directory (for database persistence)

### Expected docker-compose.yml Structure

Once Docker assets are added to the repository, the expected `docker-compose.yml` will look similar to:

```yaml
version: '3.8'

services:
  thiccdal:
    build: .
    container_name: thiccdal-app
    ports:
      - "7082:7082"  # HTTPS (or 5082:5082 for HTTP)
    volumes:
      - ./data:/app/data  # Database persistence
      - ./config:/app/config  # Configuration files
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/thiccdal.db
      - Twitch__ClientId=${TWITCH_CLIENT_ID}
      - Twitch__ClientSecret=${TWITCH_CLIENT_SECRET}
      - Twitch__RedirectUri=https://your-domain.com/auth/twitch/callback
    restart: unless-stopped
```

### Planned Volume Mounts

| Volume | Purpose | Example |
|--------|---------|---------|
| `/app/data` | SQLite database and persisted state | `-v ./data:/app/data` |
| `/app/config` | Configuration and credential files (if external) | `-v ./config:/app/config` |

### Planned Environment Variables

Configuration via environment variables (instead of appsettings.json):

```bash
# Required for Twitch OAuth
TWITCH_CLIENT_ID=your_client_id
TWITCH_CLIENT_SECRET=your_client_secret

# Network
ASPNETCORE_URLS=https://0.0.0.0:7082
ASPNETCORE_HTTPS_PORT=7082
```

### Checking for Docker Assets

To check if Docker support has been added to the repository:

```bash
git log --oneline --all -- Dockerfile docker-compose.yml
ls -la Dockerfile docker-compose.yml 2>/dev/null && echo "Docker assets found" || echo "Docker assets not yet available"
```

### Next Steps for Docker Support

When Dockerfile and docker-compose.yml are added:

1. **Build the image**: `docker-compose build`
2. **Start the container**: `docker-compose up -d`
3. **View logs**: `docker-compose logs -f thiccdal`
4. **Stop**: `docker-compose down`

---

## Configuring Platform Integrations

### Twitch Integration

Thiccdal connects to Twitch via OAuth 2.0. To set up the connection, you must register a Twitch Developer application and retrieve a **Client ID** and **Client Secret**, then configure them in `appsettings.json` before starting Thiccdal.

> **ℹ️ Current State**: Manual configuration is required today. In a future release, Thiccdal will have a first-run setup wizard where you can provide these credentials through the UI rather than editing config files by hand.

#### Getting Your Twitch Credentials (Step-by-Step)

Follow these steps to create a Twitch Developer application and retrieve your credentials:

##### 1. Go to the Twitch Developer Console

- Open [https://dev.twitch.tv/console/apps](https://dev.twitch.tv/console/apps) in your browser
- Log in with your Twitch account (this should be your **main/broadcaster account**, or an account with channel access)

##### 2. Create a New Application

- Click the **"Create Application"** button
- Fill in the form:
  - **Application Name**: Enter `Thiccdal` (or any recognizable name)
  - **OAuth Redirect URL**: Enter `https://localhost:7082/auth/twitch/callback`
    - ⚠️ **Important**: This must match exactly what you put in `appsettings.json` (see [Configuring in Thiccdal](#configuring-in-thiccdal-step-by-step) below)
  - **Category**: Choose **"Mastery & Analytics"** (or another appropriate category)
  - **Application Type**: Keep as default
- Accept the Twitch Developer Services Agreement
- Click **"Create"**

##### 3. Find Your Client ID

- The application now appears in your apps list
- Click the application name to open its details page
- On the **Manage** tab, you'll see your **Client ID** displayed at the top
- **Copy this value** — you'll need it for `appsettings.json`

##### 4. Generate a Client Secret

- On the **Manage** tab, scroll to the **Client Secret** section
- Click **"New Secret"** to generate a secret
- A new secret appears (usually hidden; click "Show" to view)
- **Copy this value immediately** and store it securely — you can only see it once
  - If you forget it, click "New Secret" again (the old one becomes invalid)
- The secret goes in `appsettings.json` as `ClientSecret`

You now have both credentials. Next, configure them in Thiccdal.

#### Configuring in Thiccdal (Step-by-Step)

###### ⚠️ Manual Configuration Required (Temporary)

**For now, you must edit `appsettings.json` by hand.** This is a temporary workaround until Thiccdal has a first-run setup UI.

1. **Locate `appsettings.json`**:
   - If you downloaded a pre-built release: look in the extracted folder
   - If you built from source: `src/Thiccdal/appsettings.json`

2. **Open the file in a text editor** (e.g., Notepad, VS Code)

3. **Find the `Twitch` section** and fill in your credentials:

   ```json
   "Twitch": {
     "ClientId": "paste_your_client_id_here",
     "ClientSecret": "paste_your_client_secret_here",
     "RedirectUri": "https://localhost:7082/auth/twitch/callback",
     ...
   }
   ```

4. **Paste your values**:
   - Replace `paste_your_client_id_here` with the Client ID you copied from Twitch Dev Console
   - Replace `paste_your_client_secret_here` with the Client Secret you generated
   - Leave `RedirectUri` as-is for local development

5. **Save the file** and close the editor

6. **Start Thiccdal**:
   ```bash
   dotnet Thiccdal.dll
   ```

**Reference: What Goes Where**

| Field in `appsettings.json` | What to Put Here | Where to Find It |
|-------|---------|---------|
| `ClientId` | Your Client ID from Twitch | [Twitch Dev Console](https://dev.twitch.tv/console/apps) → Your App → Manage tab |
| `ClientSecret` | Your Client Secret from Twitch | [Twitch Dev Console](https://dev.twitch.tv/console/apps) → Your App → Manage tab → "New Secret" |
| `RedirectUri` | Keep as `https://localhost:7082/auth/twitch/callback` for local development | Auto-configured; must match what you entered in Twitch Dev Console |

#### For Production Deployments

If you're deploying Thiccdal to a non-localhost domain:

1. **In Twitch Dev Console**, update the OAuth Redirect URL:
   - Go to your application → Manage tab
   - Change **OAuth Redirect URL** to your production domain
   - Example: `https://your-domain.com:7082/auth/twitch/callback`

2. **In `appsettings.json`**, update the `RedirectUri` to match:
   ```json
   "RedirectUri": "https://your-domain.com:7082/auth/twitch/callback"
   ```

3. Start Thiccdal with the updated configuration

#### Connecting in the Thiccdal UI

Once `appsettings.json` is configured and Thiccdal has started:

1. Open the Thiccdal control dashboard in your browser (typically `https://localhost:7082`)
2. Look for the **Twitch badge** (purple "TWI" indicator) in the top-left area
3. Click it to open the Twitch setup dialog
4. Complete the steps in the dialog:
   - **Target Channel**: Enter the Twitch channel name the bot should join
   - **Authorize with Twitch**: Click to log in and approve permissions
   - **Connect**: Establish the IRC connection to Twitch chat
5. Chat and events will now flow through Thiccdal

For detailed UI instructions, see [Connecting Thiccdal to Twitch](./connecting-to-twitch.md).

### Supported Platforms

Thiccdal is a Twitch bot and overlay:

- ✅ **Twitch** — Fully integrated (see [Connecting to Twitch](./connecting-to-twitch.md))

The adapter architecture is modular, so other platforms can be added later, but none ship today.

Check back as Thiccdal development continues. You can follow progress on [GitHub releases](https://github.com/ThindalTV/Thiccdal26/releases).

---

## First-Run Checklist

- [ ] **.NET 10 Runtime** is installed (`dotnet --version` shows 10.x)
- [ ] **Database**: `thiccdal.db` was created in your working directory on first run
- [ ] **Browser access**: You can reach `https://localhost:7082` (or the URL shown on startup)
- [ ] **Twitch OAuth credentials**: `ClientId` and `ClientSecret` are filled in `appsettings.json`
- [ ] **Twitch redirect URL**: Matches both `appsettings.json` and [Twitch Dev Console](https://dev.twitch.tv/console/apps)
- [ ] **Twitch connected**: The badge in the dashboard shows "Connected" after OAuth authorization
- [ ] **Twitch channel configured**: You selected the target channel in the Twitch connection UI
- [ ] **Chat is active**: You see messages from Twitch chat in the dashboard

---

## Common Configuration Scenarios

### Local Development (localhost)

**appsettings.json:**
```json
{
  "Logging": { "LogLevel": { "Default": "Debug" } },
  "ConnectionStrings": { "DefaultConnection": "Data Source=./data/thiccdal.db" },
  "Twitch": {
    "ClientId": "dev_client_id",
    "ClientSecret": "dev_client_secret",
    "RedirectUri": "https://localhost:7082/auth/twitch/callback"
  }
}
```

**Run:**
```bash
dotnet Thiccdal.dll
```

### Production on Windows (Network Access)

**appsettings.json:**
```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "ConnectionStrings": { "DefaultConnection": "Data Source=E:\\Streaming\\Data\\thiccdal.db" },
  "AllowedHosts": "your-machine-hostname,192.168.1.100",
  "Twitch": {
    "RedirectUri": "https://stream-pc.local:7082/auth/twitch/callback"
  }
}
```

**Run as a service** (see [Running in the Background](#running-in-the-background-windows)).

### Production in Docker (Planned)

When Docker support is available:

**docker-compose.yml** (with environment variables):
```yaml
version: '3.8'
services:
  thiccdal:
    image: thiccdal:latest
    ports:
      - "7082:7082"
    volumes:
      - ./data:/app/data
    environment:
      TWITCH_CLIENT_ID: ${TWITCH_CLIENT_ID}
      TWITCH_CLIENT_SECRET: ${TWITCH_CLIENT_SECRET}
```

**.env:**
```
TWITCH_CLIENT_ID=prod_client_id
TWITCH_CLIENT_SECRET=prod_client_secret
```

**Run:**
```bash
docker-compose up -d
```

---

## Troubleshooting & Support

### Common Issues

**Q: "Configuration error: Twitch ClientId is empty"**
- A: Verify `appsettings.json` has `"ClientId": "your_actual_id"` (not an empty string). Restart Thiccdal.

**Q: "OAuth redirect URL mismatch"**
- A: The URL you entered in `appsettings.json` must exactly match the one in [Twitch Dev Console](https://dev.twitch.tv/console/apps). Check for trailing slashes and HTTPS vs HTTP.

**Q: "Database locked" error**
- A: Close other instances of Thiccdal that have the database open, or delete `thiccdal.db` to start fresh (you'll lose chat history).

**Q: I can't connect to Thiccdal from another computer on my network**
- A: By default, Thiccdal only listens on localhost. Set `AllowedHosts` in `appsettings.json` to include your network hostname or IP, or set `ASPNETCORE_URLS=http://0.0.0.0:5082`.

**Q: Chat isn't showing up**
- A: Verify the Twitch badge shows "Connected". Check browser console (F12) for errors. Ensure Twitch scopes include `chat:read`.

### Accessing Logs

Logs are written to the console when you run `dotnet Thiccdal.dll`. To save logs to a file:

```bash
dotnet Thiccdal.dll > thiccdal.log 2>&1
```

Then inspect `thiccdal.log` for errors.

### Getting Help

- **GitHub Issues**: [Report a bug or request a feature](https://github.com/ThindalTV/Thiccdal26/issues)
- **Documentation**: Check the `/docs/help/` folder for platform-specific guides
- **Architecture Overview**: See `/docs/architecture/overview.md` for system design details

---

## What's Next?

Once Thiccdal is running and connected to your platforms:

### Before Your First Stream

1. **Understand Pre-Live Mode**: Read [Pre-Live Workflow: Preparing Your Stream](./pre-live-workflow.md)
   - Learn about the Pre-Live Checklist
   - Set up your stream title, category, and tags
   - Verify overlays and platform connections
   - Understand the Go Live confirmation flow

### After Configuration

2. **Configure the Chatbot**: Set up AI responses and memory settings (see [Chatbot Settings](./chatbot-settings.md))
3. **Configure Chat**: Set up chat integration and filters (see [Configuring Chat Settings](./configuring-chat.md) if available)
4. **Set Up the Overlay**: Add the browser source to OBS/Streamlabs (see [Using Overlays](./using-overlays.md) if available)
5. **Set Up the Teleprompter**: Add the prompter as a custom browser dock in OBS (see [Showing the teleprompter](./teleprompter-display.md))
6. **Create Bot Commands**: Define commands that respond to chat (see [Bot Commands](./bot-commands.md) if available)
7. **Configure Event Tracking**: Set up alerts for follows, subs, and redeems (see dashboard settings)

### Ready to Stream?

7. **Follow the Pre-Live Checklist**: Before each stream, use the Pre-Live Checklist (see [Pre-Live Workflow](./pre-live-workflow.md)) to ensure everything is ready
8. **Start your stream**: Tap **Go Live** when the checklist is complete

For more information, visit the [documentation hub](../architecture/overview.md).
