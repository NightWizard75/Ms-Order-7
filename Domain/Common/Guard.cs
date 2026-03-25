namespace Domain.Common;

public static class Guard
{
    public static void AgainstNegativeOrZero(int value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentException($"{parameterName} должно быть больше нуля", parameterName);
    }

    public static void AgainstNegative(int value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentException($"{parameterName} не может быть отрицательным", parameterName);
    }

    public static void AgainstNullOrEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} обязателен", parameterName);
    }
}
