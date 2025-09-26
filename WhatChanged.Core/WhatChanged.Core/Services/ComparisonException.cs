namespace WhatChanged.Core.Services;

public class ComparisonException(
    string message,
    int baselineCount,
    int currentCount,
    string? problemKey = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public int BaselineCount { get; } = baselineCount;
    public int CurrentCount { get; } = currentCount;
    public string? ProblemKey { get; } = problemKey;
}