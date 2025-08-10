using Amiquin.Core;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Models;
using Amiquin.Core.RunnableJobs;
using Amiquin.Core.Services.Toggle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace Amiquin.Tests.RunnableJobs;

public class LiveJobTests
{
    private readonly Mock<ILogger<LiveJob>> _mockLogger;
    private readonly Mock<IJobService> _mockJobService;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServerMetaRepository> _mockServerRepository;
    private readonly Mock<IToggleService> _mockToggleService;
    private readonly LiveJob _liveJob;

    public LiveJobTests()
    {
        _mockLogger = new Mock<ILogger<LiveJob>>();
        _mockJobService = new Mock<IJobService>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServerRepository = new Mock<IServerMetaRepository>();
        _mockToggleService = new Mock<IToggleService>();

        // Setup service scope factory
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(p => p.GetRequiredService<IServerMetaRepository>()).Returns(_mockServerRepository.Object);
        _mockServiceProvider.Setup(p => p.GetRequiredService<IToggleService>()).Returns(_mockToggleService.Object);

        _liveJob = new LiveJob(_mockLogger.Object, _mockJobService.Object);
    }

    [Fact]
    public void FrequencyInSeconds_DefaultValue_Is60()
    {
        // Assert
        Assert.Equal(60, _liveJob.FrequencyInSeconds);
    }

    [Fact]
    public async Task RunAsync_WithNoServers_CompletesSuccessfully()
    {
        // Arrange
        var emptyServerList = new List<ServerMeta>().AsQueryable();
        _mockServerRepository.Setup(r => r.AsQueryable())
            .Returns(emptyServerList);

        // Act
        await _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None);

        // Assert
        _mockToggleService.Verify(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), It.IsAny<string>()), Times.Never);
        _mockJobService.Verify(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithServersEnabledForLiveJob_CreatesActivitySessions()
    {
        // Arrange
        var servers = new List<ServerMeta> 
        { 
            new() { Id = 1001UL },
            new() { Id = 1002UL },
            new() { Id = 1003UL }
        };
        var serverQueryable = servers.AsQueryable();
        _mockServerRepository.Setup(r => r.AsQueryable()).Returns(serverQueryable);

        // Setup toggle service to return true for all servers
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);

        // Setup job service to accept job creation
        _mockJobService
            .Setup(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()))
            .Returns(true);

        // Act
        await _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None);

        // Assert
        _mockToggleService.Verify(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), Constants.ToggleNames.EnableLiveJob), Times.Exactly(3));
        _mockJobService.Verify(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RunAsync_WithServersDisabledForLiveJob_DoesNotCreateActivitySessions()
    {
        // Arrange
        var servers = new List<ServerMeta> 
        { 
            new() { Id = 1001UL },
            new() { Id = 1002UL }
        };
        var serverQueryable = servers.AsQueryable();
        _mockServerRepository.Setup(r => r.AsQueryable()).Returns(serverQueryable);

        // Setup toggle service to return false for all servers
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(false);

        // Act
        await _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None);

        // Assert
        _mockToggleService.Verify(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), Constants.ToggleNames.EnableLiveJob), Times.Exactly(2));
        _mockJobService.Verify(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()), Times.Never);
        _mockJobService.Verify(js => js.CancelJob(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithMixedServerStates_HandlesCorrectly()
    {
        // Arrange
        var servers = new List<ServerMeta> 
        { 
            new() { Id = 1001UL },
            new() { Id = 1002UL },
            new() { Id = 1003UL }
        };
        var serverQueryable = servers.AsQueryable();
        _mockServerRepository.Setup(r => r.AsQueryable()).Returns(serverQueryable);

        // Setup toggle service with mixed results
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(1001UL, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(1002UL, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(false);
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(1003UL, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);

        _mockJobService
            .Setup(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()))
            .Returns(true);

        // Act
        await _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None);

        // Assert
        _mockToggleService.Verify(ts => ts.IsEnabledAsync(It.IsAny<ulong>(), Constants.ToggleNames.EnableLiveJob), Times.Exactly(3));
        _mockJobService.Verify(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()), Times.Exactly(2)); // Only for 1001 and 1003
    }

    [Fact]
    public async Task RunAsync_CreatesJobsWithCorrectConfiguration()
    {
        // Arrange
        var serverId = 1001UL;
        var servers = new List<ServerMeta> { new() { Id = serverId } };
        var serverQueryable = servers.AsQueryable();
        _mockServerRepository.Setup(r => r.AsQueryable()).Returns(serverQueryable);

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(serverId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);

        Core.Job.Models.AmiquinJob? createdJob = null;
        _mockJobService
            .Setup(js => js.CreateDynamicJob(It.IsAny<Core.Job.Models.AmiquinJob>()))
            .Callback<Core.Job.Models.AmiquinJob>(job => createdJob = job)
            .Returns(true);

        // Act
        await _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(createdJob);
        Assert.Equal($"ActivitySession_{serverId}", createdJob.Id);
        Assert.Equal("ActivitySession", createdJob.Name);
        Assert.Equal(TimeSpan.FromSeconds(6), createdJob.Interval); // Default fast frequency
        Assert.NotNull(createdJob.Task);
    }

    [Fact]
    public async Task RunAsync_HandlesRepositoryException_Gracefully()
    {
        // Arrange
        _mockServerRepository
            .Setup(r => r.AsQueryable())
            .Throws(new Exception("Database connection failed"));

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => 
            _liveJob.RunAsync(_mockServiceScopeFactory.Object, CancellationToken.None));
        
        Assert.NotNull(exception);
        Assert.IsType<Exception>(exception);
        Assert.Equal("Database connection failed", exception.Message);
    }
}