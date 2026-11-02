namespace Detective.Database
{
    internal class DetectionDatabaseFactory : IDisposable
    {
        private readonly Dictionary<string, DetectionDatabase> databases = new Dictionary<string, DetectionDatabase>(StringComparer.InvariantCultureIgnoreCase);
        private bool disposedValue;

        public DetectionDatabase CreateDatabase(string fileName)
        {
            if (databases.ContainsKey(fileName))
            {
                return databases[fileName];
            }
            else
            { 
                var database = new DetectionDatabase(fileName);
                databases.Add(fileName, database);
                return database;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var database in databases.Values)
                    { 
                        database.Dispose();
                    }

                    databases.Clear();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
