using System;

namespace TurnKit
{
    internal sealed class RelayTurnTimer
    {
        private readonly Action<float, float> _onChanged;
        private readonly Action _onExpired;

        public RelayTurnTimer(Action<float, float> onChanged, Action onExpired)
        {
            _onChanged = onChanged;
            _onExpired = onExpired;
        }

        public float RemainingSeconds { get; private set; }
        public float DurationSeconds { get; private set; }
        public bool IsRunning { get; private set; }

        public void ApplyFromServer(long serverNowUtcMs, long? timerEndUtcMs)
        {
            if (!timerEndUtcMs.HasValue || serverNowUtcMs <= 0L)
            {
                Stop();
                return;
            }

            Start((timerEndUtcMs.Value - serverNowUtcMs) / 1000f);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            RemainingSeconds -= deltaTime;
            if (RemainingSeconds > 0f)
            {
                _onChanged?.Invoke(RemainingSeconds, DurationSeconds);
                return;
            }

            RemainingSeconds = 0f;
            IsRunning = false;
            _onChanged?.Invoke(RemainingSeconds, DurationSeconds);
            _onExpired?.Invoke();
        }

        public void Stop()
        {
            if (!IsRunning && RemainingSeconds <= 0f)
            {
                return;
            }

            IsRunning = false;
            RemainingSeconds = 0f;
            _onChanged?.Invoke(RemainingSeconds, DurationSeconds);
        }

        public void Reset()
        {
            IsRunning = false;
            RemainingSeconds = 0f;
            DurationSeconds = 0f;
        }

        private void Start(float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                Stop();
                return;
            }

            DurationSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
            IsRunning = true;
            _onChanged?.Invoke(RemainingSeconds, DurationSeconds);
        }
    }
}
