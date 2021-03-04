using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace System.IO
{
    /// <summary>
    /// An FileSentry, which can be used to suppress duplicate events that fire on a single change to the file.
    /// </summary>
    public class FileSentry : FileSystemWatcher, IDisposable
    {
        /// <summary>
        /// Default Watch Interval in Milliseconds
        /// </summary>
        private const int DEFAULT_WATCH_INTERVAL = 100;

        /// <summary>
        /// This Dictionary keeps the track of when an event occurred last for a particular file
        /// </summary>
        private ConcurrentDictionary<string, DateTime> _lastFileEvent;

        /// <summary>
        /// Watch Interval in Milliseconds
        /// </summary>
        private int _interval;

        /// <summary>
        /// Timespan created when interval is set
        /// </summary>
        private TimeSpan _recentTimeSpan;

        /// <summary>
        /// Interval, in milliseconds, within which events are considered "recent".
        /// </summary>
        public int Interval
        {
            get => _interval;
            set
            {
                _interval = value;

                // Set timespan based on the value passed
                _recentTimeSpan = new TimeSpan(0, 0, 0, 0, value);
            }
        }

        /// <summary>
        /// Allows user to set whether to filter recent events.
        /// If this is set a false, this class behaves like System.IO.FileSystemWatcher class.
        /// </summary>
        public bool FilterRecentEvents { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSentry"/> class.
        /// </summary>
        /// <param name="interval">The interval.</param>
        public FileSentry(int interval = DEFAULT_WATCH_INTERVAL)
        {
            Guard.Condition(interval, nameof(interval), i => i >= 0);

            InitializeMembers(interval);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSentry"/> class.
        /// </summary>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="interval">The interval.</param>
        public FileSentry(string path, int interval = DEFAULT_WATCH_INTERVAL)
            : base(path)
        {
            Guard.NotNullOrEmpty(path, nameof(path));
            Guard.Condition(interval, nameof(interval), i => i >= 0);

            InitializeMembers(interval);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSentry"/> class.
        /// </summary>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="filter">The type of files to watch. For example, "*.txt" watches for changes to all text files.</param>
        /// <param name="interval">The interval.</param>
        public FileSentry(string path, string filter, int interval = DEFAULT_WATCH_INTERVAL)
            : base(path, filter)
        {
            Guard.NotNullOrEmpty(path, nameof(path));
            Guard.NotNullOrEmpty(filter, nameof(filter));
            Guard.Condition(interval, nameof(interval), i => i >= 0);

            InitializeMembers(interval);
        }

        // These events hide the events from the base class. 
        // We want to raise these events appropriately and we do not want the 
        // users of this class subscribing to these events of the base class accidentally

        /// <inheritdoc cref="FileSystemWatcher.Changed"/>
        public new event FileSystemEventHandler Changed;

        /// <inheritdoc cref="FileSystemWatcher.Created"/>
        public new event FileSystemEventHandler Created;

        /// <inheritdoc cref="FileSystemWatcher.Deleted"/>
        public new event FileSystemEventHandler Deleted;

        /// <inheritdoc cref="FileSystemWatcher.Renamed"/>
        public new event RenamedEventHandler Renamed;

        /// <inheritdoc cref="FileSystemWatcher.OnChanged(FileSystemEventArgs)"/>
        protected new virtual void OnChanged(FileSystemEventArgs e) => Changed?.Invoke(this, e);

        /// <inheritdoc cref="FileSystemWatcher.OnCreated(FileSystemEventArgs)"/>
        protected new virtual void OnCreated(FileSystemEventArgs e) => Created?.Invoke(this, e);

        /// <inheritdoc cref="FileSystemWatcher.OnDeleted(FileSystemEventArgs)"/>
        protected new virtual void OnDeleted(FileSystemEventArgs e) => Deleted?.Invoke(this, e);

        /// <inheritdoc cref="FileSystemWatcher.OnRenamed(RenamedEventArgs)"/>
        protected new virtual void OnRenamed(RenamedEventArgs e) => Renamed?.Invoke(this, e);

        /// <summary>
        /// This Method Initializes the private members.
        /// Interval is set to its default value of 100 millisecond.
        /// FilterRecentEvents is set to true, _lastFileEvent dictionary is initialized.
        /// We subscribe to the base class events.
        /// </summary>
        private void InitializeMembers(int interval = 100)
        {
            Interval = interval;
            FilterRecentEvents = true;
            _lastFileEvent = new ConcurrentDictionary<string, DateTime>();

            base.Created += OnCreated;
            base.Changed += OnChanged;
            base.Deleted += OnDeleted;
            base.Renamed += OnRenamed;
        }

        /// <summary>
        /// This method removes all event handlers.
        /// </summary>
        private void RemoveEventHandlers()
        {
            base.Created -= OnCreated;
            base.Changed -= OnChanged;
            base.Deleted -= OnDeleted;
            base.Renamed -= OnRenamed;

            RemoveHandlers(() => Created);
            RemoveHandlers(() => Changed);
            RemoveHandlers(() => Deleted);
            RemoveHandlers(() => Renamed);
        }

        /// <summary>
        /// Simple helper method to remove event handlers via member expression
        /// </summary>
        /// <typeparam name="T">event (must be a member)</typeparam>
        /// <param name="eventMemberExpression">Function that must be accessing an event member "<c>() => this.Created</c>"</param>
        private void RemoveHandlers<T>(Expression<Func<T>> eventMemberExpression)
            where T : Delegate
        {
            if (!(eventMemberExpression?.Body is MemberExpression memberExpression))
            {
                throw new NotSupportedException($"eventMemberExpressionBody = '{eventMemberExpression?.Body?.ToString()}' - only supports 'MemberExpression'");
            }

            var eventName = memberExpression.Member.Name;
            var eventType = GetType().GetEvent(eventName);
            var @event = eventMemberExpression.Compile().Invoke();
            var eventHandlers = @event?.GetInvocationList();
            if (eventHandlers == null || eventHandlers.Length == 0)
            {
                return;
            }

            foreach (var handler in eventHandlers)
            {
                eventType.RemoveEventHandler(this, handler);
            }
        }

        /// <summary>
        /// This method searches the dictionary to find out when the last event occurred 
        /// for a particular file. If that event occurred within the specified timespan
        /// it returns true, else false
        /// </summary>
        /// <param name="fileName">The filename to be checked</param>
        /// <returns>True if an event has occurred within the specified interval, False otherwise</returns>
        private bool HasAnotherFileEventOccurredRecently(string fileName)
        {
            // Check dictionary only if user wants to filter recent events otherwise return value stays false.
            if (!FilterRecentEvents)
            {
                return false;
            }

            var retVal = false;
            if (_lastFileEvent.ContainsKey(fileName))
            {
                // If dictionary contains the filename, check how much time has elapsed
                // since the last event occurred. If the timespan is less that the 
                // specified interval, set return value to true 
                // and store current datetime in dictionary for this file
                var lastEventTime = _lastFileEvent[fileName];
                var currentTime = DateTime.Now;
                var timeSinceLastEvent = currentTime - lastEventTime;
                retVal = timeSinceLastEvent < _recentTimeSpan;
                _lastFileEvent[fileName] = currentTime;
            }
            else
            {
                // If dictionary does not contain the filename, 
                // no event has occurred in past for this file, so set return value to false
                // and append filename along with current datetime to the dictionary
                _lastFileEvent.TryAdd(fileName, DateTime.Now);
            }

            return retVal;
        }

        // Base class Event Handlers. Check if an event has occurred recently and call method
        // to raise appropriate event only if no recent event is detected

        /// <inheritdoc cref="FileSystemWatcher.OnChanged(FileSystemEventArgs)"/>
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccurredRecently(e.FullPath))
            {
                OnChanged(e);
            }
        }

        /// <inheritdoc cref="FileSystemWatcher.OnCreated(FileSystemEventArgs)"/>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccurredRecently(e.FullPath))
            {
                OnCreated(e);
            }
        }

        /// <inheritdoc cref="FileSystemWatcher.OnDeleted(FileSystemEventArgs)"/>
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccurredRecently(e.FullPath))
            {
                OnDeleted(e);
            }
        }

        /// <inheritdoc cref="FileSystemWatcher.OnRenamed(RenamedEventArgs)"/>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!HasAnotherFileEventOccurredRecently(e.OldFullPath))
            {
                OnRenamed(e);
            }
        }


        #region IDisposable Members
        private bool _disposedValue;

        /// <inheritdoc cref="FileSystemWatcher.Dispose(bool)"/>
        protected new virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    RemoveEventHandlers();
                    base.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="T:System.ComponentModel.Component" /> and remove event handlers.
        /// </summary>
        public new void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}