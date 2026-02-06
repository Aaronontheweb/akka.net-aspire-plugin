#nullable enable
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;
using Akka.Util.Internal;
using StackExchange.Redis;

namespace Aaron.Akka.Discovery.Redis.Actors
{
    /// <summary>
    /// Message to stop the discovery service
    /// </summary>
    internal sealed class StopDiscovery
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static readonly StopDiscovery Instance = new StopDiscovery();
        private StopDiscovery() { }
    }

    /// <summary>
    /// Message indicating discovery has stopped
    /// </summary>
    internal sealed class DiscoveryStopped
    {
        /// <summary>
        /// Creates a new DiscoveryStopped message
        /// </summary>
        public DiscoveryStopped(IActorRef replyTo)
        {
            ReplyTo = replyTo;
        }

        /// <summary>
        /// The actor to reply to
        /// </summary>
        public IActorRef ReplyTo { get; }
    }

    /// <summary>
    /// Message indicating discovery stop failed
    /// </summary>
    internal sealed class DiscoveryStopFailed
    {
        /// <summary>
        /// Creates a new DiscoveryStopFailed message
        /// </summary>
        public DiscoveryStopFailed(IActorRef replyTo, Exception cause)
        {
            ReplyTo = replyTo;
            Cause = cause;
        }

        /// <summary>
        /// The actor to reply to
        /// </summary>
        public IActorRef ReplyTo { get; }

        /// <summary>
        /// The exception that caused the failure
        /// </summary>
        public Exception Cause { get; }
    }

    /// <summary>
    /// The guardian actor that manages the Redis client instance and discovery operations.
    /// Instantiated by RedisServiceDiscovery as a system actor.
    /// </summary>
    internal sealed class RedisDiscoveryGuardian : UntypedActor
    {
        private sealed class Start
        {
            public static readonly Start Instance = new Start();
            private Start() { }
        }

        /// <summary>
        /// Creates Props for the RedisDiscoveryGuardian actor
        /// </summary>
        public static Props Props(RedisDiscoverySettings settings, IConnectionMultiplexer connection)
            => global::Akka.Actor.Props.Create(() => new RedisDiscoveryGuardian(settings, connection)).WithDeploy(Deploy.Local);

        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);

        private readonly ILoggingAdapter _log;
        private readonly RedisDiscoverySettings _settings;
        private readonly IConnectionMultiplexer _connection;
        private ClusterMemberRedisClient? _client;
        private readonly CancellationTokenSource _shutdownCts;

        private bool _lookingUp;
        private IActorRef? _requester;

        /// <summary>
        /// Creates a new RedisDiscoveryGuardian actor
        /// </summary>
        public RedisDiscoveryGuardian(RedisDiscoverySettings settings, IConnectionMultiplexer connection)
        {
            _settings = settings;
            _connection = connection;
            _log = Logging.GetLogger(Context.System, nameof(RedisDiscoveryGuardian));
            _shutdownCts = new CancellationTokenSource();
        }

        /// <inheritdoc/>
        protected override void PreStart()
        {
            if (_log.IsDebugEnabled)
                _log.Debug("Actor started");

            base.PreStart();
            Become(Initializing);
            Self.Tell(Start.Instance);
        }

        /// <inheritdoc/>
        protected override void PostStop()
        {
            base.PostStop();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();

            if (_log.IsDebugEnabled)
                _log.Debug("Actor stopped");
        }

        private bool Initializing(object message)
        {
            switch (message)
            {
                case Start _:
                    _client = new ClusterMemberRedisClient(_connection, _settings, _log);
                    InitializeAsync().PipeTo(Self);
                    return true;

                case Status.Success _:
                    Context.ActorOf(HeartbeatActor.Props(_settings, _client!));
                    Become(Running);

                    if (_log.IsDebugEnabled)
                        _log.Debug("Actor initialized");
                    return true;

                case Status.Failure f:
                    if (_log.IsDebugEnabled)
                        _log.Debug(f.Cause, "Failed to create/retrieve self discovery entry, retrying.");

                    InitializeAsync().PipeTo(Self);
                    return true;

                case Lookup _:
                    Sender.Tell(ImmutableList<ClusterMember>.Empty);
                    return true;

                default:
                    return false;
            }
        }

        private bool Running(object message)
        {
            switch (message)
            {
                case Lookup lookup:
                    if (_lookingUp)
                    {
                        if (_log.IsDebugEnabled)
                            _log.Debug("Another lookup operation is still underway, ignoring request.");
                        return true;
                    }

                    if (lookup.ServiceName != _settings.ServiceName)
                    {
                        _log.Error($"Lookup ServiceName mismatch. Expected: {_settings.ServiceName}, received: {lookup.ServiceName}");
                        Sender.Tell(ImmutableList<ClusterMember>.Empty);
                        return true;
                    }

                    _lookingUp = true;
                    _requester = Sender;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}", lookup.ServiceName);

                    LookupAsync().PipeTo(Self);
                    return true;

                case Status.Success result:
                    _requester?.Tell(result.Status);
                    _lookingUp = false;
                    return true;

                case Status.Failure fail:
                    _log.Warning(fail.Cause, "Failed to execute discovery lookup, retrying.");
                    LookupAsync().PipeTo(Self);
                    return true;

                case StopDiscovery _:
                    foreach (var child in Context.GetChildren())
                        Context.Stop(child);

                    var sender = Sender;
                    RemoveSelfAsync()
                        .PipeTo(Self,
                            success: () => new DiscoveryStopped(sender),
                            failure: e => new DiscoveryStopFailed(sender, e));
                    Become(Stopping);
                    return true;

                default:
                    return false;
            }
        }

        private bool Stopping(object message)
        {
            switch (message)
            {
                case Lookup _:
                    // Ignore lookup messages, we're shutting down
                    Sender.Tell(ImmutableList<ClusterMember>.Empty);
                    return true;

                case StopDiscovery _:
                    // Ignore multiple stop messages
                    Sender.Tell(global::Akka.Done.Instance);
                    return true;

                case DiscoveryStopped msg:
                    msg.ReplyTo.Tell(global::Akka.Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                case DiscoveryStopFailed fail:
                    _log.Warning(fail.Cause, "Failed to perform cleanup, node entry has not been removed from storage");
                    fail.ReplyTo.Tell(global::Akka.Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException("Should never reach this code");
        }

        private async Task<Status> InitializeAsync()
        {
            try
            {
                await _client!.GetOrCreateAsync(_shutdownCts.Token);
                return Status.Success.Instance;
            }
            catch (Exception ex)
            {
                return new Status.Failure(ex);
            }
        }

        private async Task<Status> LookupAsync()
        {
            try
            {
                var members = await _client!.GetAllAsync(_shutdownCts.Token);
                return new Status.Success(members);
            }
            catch (Exception ex)
            {
                return new Status.Failure(ex);
            }
        }

        private async Task RemoveSelfAsync()
        {
            await _client!.RemoveSelfAsync(_shutdownCts.Token);
        }
    }
}
