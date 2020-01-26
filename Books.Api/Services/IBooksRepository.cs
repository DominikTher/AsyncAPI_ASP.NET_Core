using Books.Api.Entities;
using Books.Api.ExternalModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Books.Api.Services
{
    public interface IBooksRepository
    {
        IEnumerable<Book> GetBooks();
        Task<IEnumerable<Book>> GetBooksAsync();
        Task<Book> GetBookAsync(Guid bookId);
        Task<IEnumerable<Book>> GetBooksAsync(IEnumerable<Guid> bookIds);
        void AddBook(Book bookToAdd);
        Task<BookCover> GetBookCoverAsync(string coverId);
        Task<IEnumerable<BookCover>> GetBookCoversAsync(Guid bookId);
        Task<bool> SaveChangesAsync();
    }
}
