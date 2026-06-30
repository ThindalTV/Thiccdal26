using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class StreamingConfigValidatorTests
{
    public sealed class ValidateIngestUrl
    {
        [Theory]
        [InlineData("rtmp://localhost:1935/live")]
        [InlineData("rtmp://0.0.0.0:1935/live")]
        [InlineData("rtmps://streaming.example.com:1936/mystream")]
        [InlineData("rtmp://192.168.1.100:1935/obs")]
        public void WhenValidRtmpUrl_ThenReturnsTrue(string url)
        {
            bool result = StreamingConfigValidator.ValidateIngestUrl(url, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenUrlIsEmpty_ThenReturnsFalse(string? url)
        {
            bool result = StreamingConfigValidator.ValidateIngestUrl(url, out string? error);

            Assert.False(result);
            Assert.Equal("Ingest URL is required.", error);
        }

        [Fact]
        public void WhenUrlIsNotAbsolute_ThenReturnsFalse()
        {
            bool result = StreamingConfigValidator.ValidateIngestUrl("not-a-url", out string? error);

            Assert.False(result);
            Assert.Equal("Ingest URL must be a valid absolute URL.", error);
        }

        [Theory]
        [InlineData("http://localhost:1935/live")]
        [InlineData("https://localhost:1935/live")]
        [InlineData("tcp://localhost:1935/live")]
        public void WhenUrlHasWrongScheme_ThenReturnsFalse(string url)
        {
            bool result = StreamingConfigValidator.ValidateIngestUrl(url, out string? error);

            Assert.False(result);
            Assert.Equal("Ingest URL must use rtmp:// or rtmps:// scheme.", error);
        }

        [Theory]
        [InlineData("rtmp://localhost:1935")]
        [InlineData("rtmp://localhost:1935/")]
        public void WhenUrlHasNoStreamPath_ThenReturnsFalse(string url)
        {
            bool result = StreamingConfigValidator.ValidateIngestUrl(url, out string? error);

            Assert.False(result);
            Assert.Contains("stream path", error);
        }
    }

    public sealed class ValidateExternalHost
    {
        [Theory]
        [InlineData("localhost")]
        [InlineData("rtmp-server.local")]
        [InlineData("192.168.1.100")]
        [InlineData("my-rtmp-host")]
        public void WhenValidHostname_ThenReturnsTrue(string host)
        {
            bool result = StreamingConfigValidator.ValidateExternalHost(host, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenHostIsEmpty_ThenReturnsFalse(string? host)
        {
            bool result = StreamingConfigValidator.ValidateExternalHost(host, out string? error);

            Assert.False(result);
            Assert.Contains("required", error);
        }

        [Theory]
        [InlineData("http://localhost")]
        [InlineData("rtmp://server.local")]
        public void WhenHostContainsScheme_ThenReturnsFalse(string host)
        {
            bool result = StreamingConfigValidator.ValidateExternalHost(host, out string? error);

            Assert.False(result);
            Assert.Contains("hostname or IP", error);
        }
    }

    public sealed class ValidateApiPort
    {
        [Theory]
        [InlineData(1)]
        [InlineData(80)]
        [InlineData(5100)]
        [InlineData(65535)]
        public void WhenValidPort_ThenReturnsTrue(int port)
        {
            bool result = StreamingConfigValidator.ValidateApiPort(port, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        [InlineData(100000)]
        public void WhenInvalidPort_ThenReturnsFalse(int port)
        {
            bool result = StreamingConfigValidator.ValidateApiPort(port, out string? error);

            Assert.False(result);
            Assert.Contains("between 1 and 65535", error);
        }
    }

    public sealed class ValidateApiKey
    {
        [Theory]
        [InlineData("1234567890123456")]
        [InlineData("MySecureApiKey123456")]
        [InlineData("abcdefghijklmnopqrstuvwxyz123456")]
        public void WhenValidApiKey_ThenReturnsTrue(string apiKey)
        {
            bool result = StreamingConfigValidator.ValidateApiKey(apiKey, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenApiKeyIsEmpty_ThenReturnsFalse(string? apiKey)
        {
            bool result = StreamingConfigValidator.ValidateApiKey(apiKey, out string? error);

            Assert.False(result);
            Assert.Contains("required", error);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("123456789012345")]
        public void WhenApiKeyTooShort_ThenReturnsFalse(string apiKey)
        {
            bool result = StreamingConfigValidator.ValidateApiKey(apiKey, out string? error);

            Assert.False(result);
            Assert.Contains("at least 16 characters", error);
        }
    }

    public sealed class ValidateRecordingPath
    {
        [Theory]
        [InlineData("/recordings")]
        [InlineData("/var/recordings/thiccdal")]
        [InlineData("C:\\Recordings")]
        public void WhenValidPath_ThenReturnsTrue(string path)
        {
            bool result = StreamingConfigValidator.ValidateRecordingPath(path, isRequired: false, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenPathIsEmptyAndNotRequired_ThenReturnsTrue(string? path)
        {
            bool result = StreamingConfigValidator.ValidateRecordingPath(path, isRequired: false, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void WhenPathIsEmptyAndRequired_ThenReturnsFalse(string? path)
        {
            bool result = StreamingConfigValidator.ValidateRecordingPath(path, isRequired: true, out string? error);

            Assert.False(result);
            Assert.Contains("required", error);
        }
    }

    public sealed class ValidateFfmpegPath
    {
        [Theory]
        [InlineData("ffmpeg")]
        [InlineData("/usr/bin/ffmpeg")]
        [InlineData("C:\\ffmpeg\\bin\\ffmpeg.exe")]
        public void WhenValidPath_ThenReturnsTrue(string path)
        {
            bool result = StreamingConfigValidator.ValidateFfmpegPath(path, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenPathIsEmpty_ThenReturnsFalse(string? path)
        {
            bool result = StreamingConfigValidator.ValidateFfmpegPath(path, out string? error);

            Assert.False(result);
            Assert.Contains("required", error);
        }
    }

    public sealed class ValidateBrbSlatePath
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WhenPathIsEmpty_ThenReturnsTrue(string? path)
        {
            bool result = StreamingConfigValidator.ValidateBrbSlatePath(path, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("/media/brb.png")]
        [InlineData("/media/brb.jpg")]
        [InlineData("/media/brb.jpeg")]
        [InlineData("/media/brb.mp4")]
        [InlineData("/media/brb.webm")]
        [InlineData("/media/brb.mov")]
        public void WhenValidImageOrVideoPath_ThenReturnsTrue(string path)
        {
            bool result = StreamingConfigValidator.ValidateBrbSlatePath(path, out string? error);

            Assert.True(result);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("/media/brb.txt")]
        [InlineData("/media/brb.pdf")]
        [InlineData("/media/brb.exe")]
        public void WhenInvalidExtension_ThenReturnsFalse(string path)
        {
            bool result = StreamingConfigValidator.ValidateBrbSlatePath(path, out string? error);

            Assert.False(result);
            Assert.Contains("image or video", error);
        }
    }
}
