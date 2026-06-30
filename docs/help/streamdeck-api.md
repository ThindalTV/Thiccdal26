# Stream Deck API Documentation

## Overview

The Stream Deck API is a REST API designed specifically for Stream Deck integration, enabling seamless control of your streaming workflow from physical Stream Deck buttons. This API provides endpoints for managing streaming status, restream control, teleprompter navigation, overlays, questions, chat, and operator modes.

All endpoints are optimized for Stream Deck compatibility and accept empty bodies for POST requests when no parameters are required.

## Base URL

```
/api/streamdeck
```

**Full URL Format:**
```
http://[your-server]:[port]/api/streamdeck
```

Replace `[your-server]` with your server's hostname or IP address, and `[port]` with your API port (typically 5000 for HTTP or 5001 for HTTPS).

## Response Format

All Stream Deck API endpoints return a standardized JSON envelope with the following structure:

```json
{
  "success": true|false,
  "message": "Human-readable message",
  "data": {},
  "error": null|"Error details if applicable"
}
```

### Response Envelope Fields

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Indicates whether the request was successful |
| `message` | string | Human-readable status message |
| `data` | object | Response payload (structure varies by endpoint) |
| `error` | string \| null | Error details if the request failed |

### Success Response Example

```json
{
  "success": true,
  "message": "Go-live workflow executed",
  "data": null,
  "error": null
}
```

### Error Response Example

```json
{
  "success": false,
  "message": "Streaming failed",
  "data": null,
  "error": "Connection timeout"
}
```

---

## Endpoint Groups

### Streaming Control

Manage your main streaming status and control go-live and stop workflows.

#### Get Streaming Status

Retrieve the current streaming status and state.

**Endpoint:** `GET /api/streamdeck/streaming/status`

**Method:** GET  
**Authentication:** Not required  
**Query Parameters:** None

**Response:**
```json
{
  "success": true,
  "message": "OK",
  "data": {
    "isRunning": true,
    "state": "Started"
  },
  "error": null
}
```

**Response Fields (data):**
| Field | Type | Description |
|-------|------|-------------|
| `isRunning` | boolean | Whether streaming is currently active |
| `state` | string | Current streaming state (e.g., "Started", "Stopped") |

**Example curl:**
```bash
curl -X GET "http://localhost:5000/api/streamdeck/streaming/status"
```

---

#### Go Live

Execute the complete go-live workflow, preparing all systems for streaming.

**Endpoint:** `POST /api/streamdeck/streaming/go-live`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Go-live workflow executed",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/streaming/go-live"
```

**Common Use Case:** Press a Stream Deck button to start your complete streaming workflow in one action.

---

#### Stop Streaming

Stop the active stream.

**Endpoint:** `POST /api/streamdeck/streaming/stop`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Streaming stopped",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/streaming/stop"
```

---

### Restream Control

Manage your restream status and control which platforms are currently active.

#### Get Restream Status

Retrieve the current restream status and enabled platforms.

**Endpoint:** `GET /api/streamdeck/restream/status`

**Method:** GET  
**Authentication:** Not required  
**Query Parameters:** None

**Response:**
```json
{
  "success": true,
  "message": "OK",
  "data": {
    "isRunning": true,
    "destinations": [
      {
        "platformName": "Twitch",
        "isEnabled": true
      },
      {
        "platformName": "YouTube",
        "isEnabled": false
      }
    ]
  },
  "error": null
}
```

**Example curl:**
```bash
curl -X GET "http://localhost:5000/api/streamdeck/restream/status"
```

---

#### Start Restream

Start restreaming to all enabled destinations.

**Endpoint:** `POST /api/streamdeck/restream/start`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Restream started",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/restream/start"
```

---

#### Stop Restream

Stop restreaming to all destinations.

**Endpoint:** `POST /api/streamdeck/restream/stop`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Restream stopped",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/restream/stop"
```

---

#### Enable Platform

Enable restreaming to a specific platform.

**Endpoint:** `POST /api/streamdeck/restream/destinations/{platform}/enable`

**Method:** POST  
**Authentication:** Not required  
**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `platform` | string | Platform name (e.g., "Twitch", "YouTube", "Facebook") |

**Response:**
```json
{
  "success": true,
  "message": "Twitch enabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/restream/destinations/Twitch/enable"
```

---

#### Disable Platform

Disable restreaming to a specific platform.

**Endpoint:** `POST /api/streamdeck/restream/destinations/{platform}/disable`

**Method:** POST  
**Authentication:** Not required  
**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `platform` | string | Platform name (e.g., "Twitch", "YouTube", "Facebook") |

**Response:**
```json
{
  "success": true,
  "message": "YouTube disabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/restream/destinations/YouTube/disable"
```

---

#### Toggle Platform

Toggle the restream status for a specific platform (enable if disabled, disable if enabled).

**Endpoint:** `POST /api/streamdeck/restream/destinations/{platform}/toggle`

**Method:** POST  
**Authentication:** Not required  
**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `platform` | string | Platform name (e.g., "Twitch", "YouTube", "Facebook") |

**Response:**
```json
{
  "success": true,
  "message": "Twitch disabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/restream/destinations/Twitch/toggle"
```

**Common Use Case:** Create a single Stream Deck button that toggles a platform on or off without needing to know the current state.

---

### Teleprompter Control

Navigate teleprompter content with simple scroll commands.

#### Scroll Up

Scroll the teleprompter content upward.

**Endpoint:** `POST /api/streamdeck/teleprompter/scroll/up`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Scrolled up",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/teleprompter/scroll/up"
```

**Common Use Case:** Create Stream Deck buttons for hands-free teleprompter control while streaming.

---

#### Scroll Down

Scroll the teleprompter content downward.

**Endpoint:** `POST /api/streamdeck/teleprompter/scroll/down`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Scrolled down",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/teleprompter/scroll/down"
```

---

### Overlay Control

Manage and test overlay components.

#### Get Overlay Components

Retrieve a list of available overlay components.

**Endpoint:** `GET /api/streamdeck/overlay/components`

**Method:** GET  
**Authentication:** Not required  
**Query Parameters:** None

**Response:**
```json
{
  "success": true,
  "message": "OK",
  "data": [
    "Timer",
    "SceneIndicator",
    "ChatFeed",
    "DonationCounter"
  ],
  "error": null
}
```

**Example curl:**
```bash
curl -X GET "http://localhost:5000/api/streamdeck/overlay/components"
```

---

#### Test Overlay Component

Trigger a test display of a specific overlay component.

**Endpoint:** `POST /api/streamdeck/overlay/{componentName}/test`

**Method:** POST  
**Authentication:** Not required  
**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `componentName` | string | Name of the overlay component to test |

**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Triggered test for Timer",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/overlay/Timer/test"
```

**Common Use Case:** Quickly verify overlays are working correctly before or during a stream.

---

### Questions Control

Manage viewer questions and auto-detection settings.

#### Get Questions

Retrieve the current question dashboard state.

**Endpoint:** `GET /api/streamdeck/questions`

**Method:** GET  
**Authentication:** Not required  
**Query Parameters:** None

**Response:**
```json
{
  "success": true,
  "message": "OK",
  "data": {
    "totalQuestions": 15,
    "waitingQuestions": 12,
    "selectedQuestion": {
      "id": "q123",
      "text": "What's your streaming setup?",
      "author": "viewer123"
    },
    "liveQuestion": null,
    "autoDetectEnabled": true
  },
  "error": null
}
```

**Example curl:**
```bash
curl -X GET "http://localhost:5000/api/streamdeck/questions"
```

---

#### Promote Next Question

Promote the currently selected question to display as the live question.

**Endpoint:** `POST /api/streamdeck/questions/next`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Next question promoted",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/next"
```

**Common Use Case:** Press a Stream Deck button to quickly promote the next question in your queue.

---

#### Dismiss Live Question

Dismiss the currently displayed live question.

**Endpoint:** `POST /api/streamdeck/questions/dismiss`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Live question dismissed",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/dismiss"
```

---

#### Clear Waiting Questions

Clear all questions currently waiting in the queue.

**Endpoint:** `POST /api/streamdeck/questions/clear`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Waiting questions cleared",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/clear"
```

---

#### Enable Auto-Detect

Enable automatic question detection.

**Endpoint:** `POST /api/streamdeck/questions/autodetect/enable`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Auto-detect enabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/autodetect/enable"
```

---

#### Disable Auto-Detect

Disable automatic question detection.

**Endpoint:** `POST /api/streamdeck/questions/autodetect/disable`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Auto-detect disabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/autodetect/disable"
```

---

#### Toggle Auto-Detect

Toggle automatic question detection (enable if disabled, disable if enabled).

**Endpoint:** `POST /api/streamdeck/questions/autodetect/toggle`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Auto-detect enabled",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/questions/autodetect/toggle"
```

---

### Chat Control

Send messages to chat.

#### Send Chat Message

Send a message to the chat.

**Endpoint:** `POST /api/streamdeck/chat/send`

**Method:** POST  
**Authentication:** Not required  
**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `message` | string | Yes | The message text to send |

**Response:**
```json
{
  "success": true,
  "message": "Message sent",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/chat/send?message=Hello%20chat!"
```

**Example curl (with special characters):**
```bash
curl -X POST 'http://localhost:5000/api/streamdeck/chat/send?message=Thanks%20for%20watching%20%3A%29'
```

**Common Use Case:** Send quick responses or announcements with a Stream Deck button (use URL encoding for special characters).

---

### Operator Mode Control

Check and set the current operator mode.

#### Get Operator Mode

Retrieve the current operator mode.

**Endpoint:** `GET /api/streamdeck/operator/mode`

**Method:** GET  
**Authentication:** Not required  
**Query Parameters:** None

**Response:**
```json
{
  "success": true,
  "message": "OK",
  "data": {
    "mode": "Live"
  },
  "error": null
}
```

**Response Fields (data):**
| Field | Type | Description |
|-------|------|-------------|
| `mode` | string | Current mode ("PreLive" or "Live") |

**Example curl:**
```bash
curl -X GET "http://localhost:5000/api/streamdeck/operator/mode"
```

---

#### Set PreLive Mode

Switch to PreLive mode for pre-stream preparation.

**Endpoint:** `POST /api/streamdeck/operator/mode/prelive`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Mode set to PreLive",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/operator/mode/prelive"
```

---

#### Set Live Mode

Switch to Live mode for active streaming.

**Endpoint:** `POST /api/streamdeck/operator/mode/live`

**Method:** POST  
**Authentication:** Not required  
**Body:** Empty (or can omit body entirely)

**Response:**
```json
{
  "success": true,
  "message": "Mode set to Live",
  "data": null,
  "error": null
}
```

**Example curl:**
```bash
curl -X POST "http://localhost:5000/api/streamdeck/operator/mode/live"
```

---

## Stream Deck Setup Instructions

### Prerequisites

- Stream Deck device (any model with HTTP request support)
- Stream Deck software installed on your computer
- Network connectivity between your Stream Deck and the API server
- API server running and accessible on your network

### Step-by-Step Setup

#### 1. Open Stream Deck Software

Launch the Stream Deck application on your computer.

#### 2. Add HTTP Request Action

1. Create a new profile or edit an existing one
2. Find the "System" category in the actions library
3. Drag the **"HTTP Request"** action onto a button
4. Release to create the action

#### 3. Configure the HTTP Request

Click on the HTTP Request button to open the settings panel:

**Basic Configuration:**
- **Request URL:** Enter your API endpoint (e.g., `http://localhost:5000/api/streamdeck/streaming/go-live`)
- **Request Method:** Select GET or POST based on the endpoint
- **On Successful Request:** Choose "No Action" or "Show OK" notification
- **On Failed Request:** Choose "Show Error" notification

**Example Configurations:**

**Button 1 - Go Live:**
```
URL: http://your-server:5000/api/streamdeck/streaming/go-live
Method: POST
```

**Button 2 - Stop Streaming:**
```
URL: http://your-server:5000/api/streamdeck/streaming/stop
Method: POST
```

**Button 3 - Check Streaming Status:**
```
URL: http://your-server:5000/api/streamdeck/streaming/status
Method: GET
```

**Button 4 - Next Question:**
```
URL: http://your-server:5000/api/streamdeck/questions/next
Method: POST
```

**Button 5 - Toggle Twitch:**
```
URL: http://your-server:5000/api/streamdeck/restream/destinations/Twitch/toggle
Method: POST
```

#### 4. Test Your Configuration

1. Click the button on your Stream Deck device
2. Verify the action executes (check server logs if available)
3. Look for success/error notifications from Stream Deck

#### 5. Customize Button Appearance

You can customize button appearance in Stream Deck:
- Add custom icon or image
- Set a title (e.g., "Go Live", "Stop", "Next Question")
- Configure long-press behavior for additional actions
- Set up multi-action sequences

### Network Configuration Tips

**Local Network:**
If your API is on the same local network:
```
http://[local-ip]:5000/api/streamdeck/endpoint
```

**Remote Access:**
If you need remote access, set up a reverse proxy or use a domain name with appropriate security:
```
https://[your-domain]/api/streamdeck/endpoint
```

### Troubleshooting

**"Connection Failed" Error:**
- Verify the API server is running
- Check the URL is correct (no typos)
- Ensure firewall allows connections to your API port
- Test with `curl` command to verify endpoint works

**No Response from API:**
- Check server logs for errors
- Verify API port is accessible from your Stream Deck device
- Test network connectivity: `ping [server-ip]`

**Button Press Not Triggering Action:**
- Verify HTTP Request action is properly configured
- Check URL format and method (GET vs POST)
- Test in Stream Deck settings before deploying to device

---

## Integration Examples

### Example Workflow: Complete Stream Startup

Create a multi-action sequence to go live with multiple platforms:

1. **Button 1 (Single Press):** Execute go-live workflow
   ```
   POST /api/streamdeck/streaming/go-live
   ```

2. **Button 2 (Long Press):** Start restreaming
   ```
   POST /api/streamdeck/restream/start
   ```

### Example Workflow: Question Management

Create a series of buttons for managing viewer questions:

- **Button A:** Get Questions (GET)
- **Button B:** Promote Next Question (POST)
- **Button C:** Dismiss Question (POST)
- **Button D:** Clear All Questions (POST)

### Example Workflow: Platform Toggle

Create individual buttons to manage which platforms are active:

- **Button 1:** Toggle Twitch
  ```
  POST /api/streamdeck/restream/destinations/Twitch/toggle
  ```

- **Button 2:** Toggle YouTube
  ```
  POST /api/streamdeck/restream/destinations/YouTube/toggle
  ```

- **Button 3:** Toggle Facebook
  ```
  POST /api/streamdeck/restream/destinations/Facebook/toggle
  ```

---

## Rate Limiting & Best Practices

### Rate Limiting

While there are no explicit rate limits on these endpoints, avoid excessive polling:

- **Status checks:** Every 5-10 seconds maximum
- **Actions:** Triggered by user interaction (no automated loops)
- **Chat messages:** Reasonable delays between messages

### Best Practices

1. **Use POST for actions** that change state (starting/stopping, enabling/disabling)
2. **Use GET for queries** that retrieve information
3. **Minimize polling** of status endpoints - only check when needed
4. **Include error handling** in Stream Deck configurations
5. **Test URLs locally** before configuring Stream Deck
6. **Document your button layout** for future reference

---

## Error Handling

### Common Error Scenarios

**Empty Message (Chat Endpoint):**
```json
{
  "success": false,
  "message": "Message cannot be empty",
  "data": null,
  "error": "Message cannot be empty"
}
```

**Platform Not Found (Restream):**
```json
{
  "success": false,
  "message": "Platform not found",
  "data": null,
  "error": "Platform 'InvalidPlatform' not found"
}
```

**Service Unavailable:**
```json
{
  "success": false,
  "message": "Service unavailable",
  "data": null,
  "error": "Connection failed"
}
```

### Handling Errors in Stream Deck

Stream Deck can be configured to:
1. Show error notifications when requests fail
2. Trigger alternative actions on failure
3. Log errors for debugging

---

## Support & Troubleshooting

For issues or questions:

1. Check the endpoint URL format
2. Verify the API server is running
3. Test with curl commands
4. Check network connectivity
5. Review API server logs
6. Verify your Stream Deck device is updated
7. Test in Stream Deck settings panel

---

## Changelog

**Version 1.0**
- Initial API documentation
- All core endpoints documented
- Stream Deck setup instructions included
- Example configurations provided
