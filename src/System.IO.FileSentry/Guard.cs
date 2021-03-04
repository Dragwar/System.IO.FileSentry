namespace System.IO
{
    internal static class Guard
    {
        public static T Condition<T>(T value, string parameterName, Predicate<T> condition)
        {
            NotNull(condition, nameof(condition));
            NotNull(value, nameof(value));

            if (!condition(value))
            {
                NotNullOrEmpty(parameterName, nameof(parameterName));
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        public static T NotNull<T>(T value, string parameterName)
        {
            if (ReferenceEquals(value, null))
            {
                NotNullOrEmpty(parameterName, nameof(parameterName));
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static string NotNullOrEmpty(string value, string parameterName)
        {
            Exception e = null;
            if (value is null)
            {
                e = new ArgumentNullException(parameterName);
            }
            else if (value.Trim().Length == 0)
            {
                e = new ArgumentException($"The string argument '{parameterName}' cannot be empty.");
            }

            if (e != null)
            {
                NotNullOrEmpty(parameterName, nameof(parameterName));
                throw e;
            }

            return value;
        }
    }
}