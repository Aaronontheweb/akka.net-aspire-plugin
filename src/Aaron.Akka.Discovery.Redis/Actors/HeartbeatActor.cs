#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;

namespace Aaron.Akka.Discovery.Redis.Actors
{
    /// <summary>
    /// Manages the TTL heartbeat that updates the Redis entry for this cluster node.
    /// Instantiated as a child of the RedisDiscoveryGuardian actor.
    /// </summary>
    internal sealed class HeartbeatActor : UntypedActor, IWithTimers
    {
        /// <summary>
        /// Creates Props for the HeartbeatActor
        /// </summary>
        public static Props Props(RedisDiscoverySettings settings, ClusterMemberRedisClient client)
            => global::Akka.Actor.Props.Create(() => new HeartbeatActor(settings, client)).WithDeploy(Deploy.Local);

        private readonly string _heartbeatTimerKey = "heartbeat-key";
        private readonly string _heartbeat = "heartbeat";
        private readonly ILoggingAdapter _log;
        private readonly ClusterMemberRedisClient _client;
        private readonly TimeSpan _heartbeatInterval;
        private readonly CancellationTokenSource _shutdownCts;

        private bool _updating;

        /// <summary>
        /// Creates a new HeartbeatActor
        /// </summary>
        public HeartbeatActor(RedisDiscoverySettings settings, ClusterMemberRedisClient client)
        {
            _client = client;
            _heartbeatInterval = settings.TtlHeartbeatInterval;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();
        }

        /// <inheritdoc/>
        protected override void PreStart()
        {
            Timers!.StartPeriodicTimer(_heartbeatTimerKey, _heartbeat, _heartbeatInterval);
        }

        /// <inheritdoc/>
        protected override void PostStop()
        {
            Timers!.CancelAll();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case string str when str == _heartbeat:
                    if (_updating)
                        break;

                    _updating = true;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Updating cluster member entry TTL");

                    ExecuteUpdateAsync().PipeTo(Self);
                    break;

                case Status.Success _:
                    _updating = false;
                    break;

                case Status.Failure f:
                    if (_shutdownCts.IsCancellationRequested)
                    {
                        _log.Warning(f.Cause, "Failed to update cluster member entry");
                        return;
                    }

                    _log.Warning(f.Cause, "Failed to update TTL heartbeat, retrying");
                    ExecuteUpdateAsync().PipeTo(Self);
                    break;

                default:
                    Unhandled(message);
                    break;
            }
        }

        private async Task<Status> ExecuteUpdateAsync()
        {
            try
            {
                await _client.UpdateAsync(_shutdownCts.Token);
                return Status.Success.Instance;
            }
            catch (Exception ex)
            {
                return new Status.Failure(ex);
            }
        }

        /// <summary>
        /// Timer scheduler for periodic heartbeat
        /// </summary>
        public ITimerScheduler? Timers { get; set; }
    }
}
