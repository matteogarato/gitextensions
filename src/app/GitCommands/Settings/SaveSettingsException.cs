namespace GitCommands.Settings;

public class SaveSettingsException(Exception? innerException) : Exception(null, innerException);
