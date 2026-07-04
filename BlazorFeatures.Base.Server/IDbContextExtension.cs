using Microsoft.EntityFrameworkCore;

namespace BlazorFeatures.Base.Server
{
    public interface IDbContextExtension
    {
        public static abstract void OnModelCreating(ModelBuilder builder);
    }
}
