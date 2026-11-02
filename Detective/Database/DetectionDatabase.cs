using Detective.Database.Entities;
using LiteDB;

namespace Detective.Database
{
    public class DetectionDatabase : IDisposable
    {
        private readonly LiteDatabase database;
        private readonly ILiteCollection<LabelEntity> labels;
        private readonly ILiteCollection<FileEntity> files;
        private bool disposedValue;

        public ILiteCollection<LabelEntity> Labels => labels;

        public ILiteCollection<FileEntity> Files => files;

        public DetectionDatabase(string fileName)
        {
            database = new LiteDatabase(fileName);
            labels = database.GetCollection<LabelEntity>();
            labels.EnsureIndex(x => x.Name, true);
            files = database.GetCollection<FileEntity>();
        }

        public void Commit()
        { 
            database.Commit();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    database.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
