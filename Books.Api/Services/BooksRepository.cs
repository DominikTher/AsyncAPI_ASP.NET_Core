using Books.Api.Contexts;
using Books.Api.Entities;
using Books.Api.ExternalModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Books.Api.Services
{
    public class BooksRepository : IBooksRepository, IDisposable
    {
        private BooksContext booksContext;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<BooksRepository> logger;
        private bool disposed = false;
        private CancellationTokenSource cancellationTokenSource;

        public BooksRepository(BooksContext booksContext, IHttpClientFactory httpClientFactory, ILogger<BooksRepository> logger)
        {
            this.booksContext = booksContext ?? throw new ArgumentNullException(nameof(booksContext));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<Book> GetBooks()
        {
            booksContext.Database.ExecuteSqlRaw("WAITFOR DELAY '00:00:02';");

            return booksContext.Books
                                .Include(book => book.Author)
                                .ToList();
        }

        public async Task<IEnumerable<Book>> GetBooksAsync()
        {
            await booksContext.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:02';");

            return await booksContext.Books
                                        .Include(book => book.Author)
                                        .ToListAsync();
        }

        public async Task<Book> GetBookAsync(Guid bookId)
        {
            //var pageCalculator = new Legacy.ComplicatedPageCalculator();
            //var amountOfPages = pageCalculator.CalculateBookPages();

            // Pitfall #1: using Task.Run() on the server
            //logger.LogInformation($"ThreadId when entering GetBookAsync: {Thread.CurrentThread.ManagedThreadId}");
            //var bookPages = await GetBookPages();

            return await booksContext.Books
                                        .Include(book => book.Author)
                                        .FirstOrDefaultAsync(book => book.Id == bookId);
        }

        // Pitfall #1: using Task.Run() on the server
        //private Task<int> GetBookPages()
        //{
        //    return Task.Run(() =>
        //    {
        //        logger.LogInformation($"ThreadId when calculating the amount of pages: {Thread.CurrentThread.ManagedThreadId}");

        //        var pageCalculator = new Books.Legacy.ComplicatedPageCalculator();
        //        return pageCalculator.CalculateBookPages();
        //    });
        //}

        // Piftall #3: modifying shared state
        // note: using HttpClient directly for readability purposes. 
        // It's better to initialize the client via _httpClientFactory, 
        // eg on constructing

        //private HttpClient _httpClient = new HttpClient();

        //public async Task<IEnumerable<BookCover>> DownloadBookCoverAsync(Guid bookId)
        //{
        //    var bookCoverUrls = new[]
        //    {
        //        $"http://localhost:52644/api/bookcovers/{bookId}-dummycover1",
        //        $"http://localhost:52644/api/bookcovers/{bookId}-dummycover2"
        //    };

        //    var bookCovers = new List<BookCover>();
        //    var downloadTask1 = DownloadBookCoverAsync(bookCoverUrls[0], bookCovers); // Different thread but same object, might cause problems!!
        //    var downloadTask2 = DownloadBookCoverAsync(bookCoverUrls[1], bookCovers); // Different thread but same object, might cause problems!!

        //    await Task.WhenAll(downloadTask1, downloadTask2);

        //    return bookCovers;
        //}

        //// Piftall #3: modifying shared state
        //private async Task DownloadBookCoverAsync(string bookCoverUrl, List<BookCover> bookCovers)
        //{
        //    var response = await _httpClient.GetAsync(bookCoverUrl);
        //    var bookCover = JsonConvert.DeserializeObject<BookCover>(
        //            await response.Content.ReadAsStringAsync());

        //    bookCovers.Add(bookCover);
        //}


        public async Task<IEnumerable<Entities.Book>> GetBooksAsync(IEnumerable<Guid> bookIds)
        {
            return await booksContext.Books.Where(b => bookIds.Contains(b.Id)).Include(b => b.Author).ToListAsync();
        }

        public void AddBook(Book bookToAdd)
        {
            if (bookToAdd == null)
            {
                throw new ArgumentNullException(nameof(bookToAdd));
            }

            booksContext.Books.Add(bookToAdd);
        }

        public async Task<BookCover> GetBookCoverAsync(string coverId)
        {
            var httpClient = httpClientFactory.CreateClient();

            // pass through a dummy name
            var response = await httpClient.GetAsync($"http://localhost:51288/api/bookcovers/{coverId}");

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<BookCover>(
                    await response.Content.ReadAsStringAsync());
            }

            return null;
        }

        public async Task<IEnumerable<BookCover>> GetBookCoversAsync(Guid bookId)
        {
            var httpClient = httpClientFactory.CreateClient();
            var bookCovers = new List<BookCover>();
            cancellationTokenSource = new CancellationTokenSource();

            // create a list of fake bookcovers
            var bookCoverUrls = new[]
            {
                $"http://localhost:51288/api/bookcovers/{bookId}-dummycover1",
                //$"http://localhost:51288/api/bookcovers/{bookId}-dummycover2?returnFault=true",
                $"http://localhost:51288/api/bookcovers/{bookId}-dummycover3",
                $"http://localhost:51288/api/bookcovers/{bookId}-dummycover4",
                $"http://localhost:51288/api/bookcovers/{bookId}-dummycover5"
            };

            //foreach (var bookCoverUrl in bookCoverUrls)
            //{
            //    var response = await httpClient
            //       .GetAsync(bookCoverUrl);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        bookCovers.Add(JsonConvert.DeserializeObject<BookCover>(
            //            await response.Content.ReadAsStringAsync()));
            //    }
            //}

            // create the tasks
            var downloadBookCoverTasksQuery = bookCoverUrls.Select(bookCoverUrl => DownloadBookCoverAsync(httpClient, bookCoverUrl, cancellationTokenSource.Token));

            // start the tasks
            var downloadBookCoverTasks = downloadBookCoverTasksQuery.ToList();

            //return bookCovers;

            try
            {
                return await Task.WhenAll(downloadBookCoverTasks);
            }
            catch (OperationCanceledException operationCanceledException)
            {
                logger.LogInformation($"{operationCanceledException.Message}");
                foreach (var task in downloadBookCoverTasks)
                {
                    logger.LogInformation($"Task {task.Id} has status {task.Status}");
                }

                return new List<BookCover>();
            }
            catch (Exception exception)
            {
                logger.LogError($"{exception.Message}");
                throw;
            }
        }

        private async Task<BookCover> DownloadBookCoverAsync(HttpClient httpClient, string bookCoverUrl, CancellationToken cancellationToken)
        {
            //throw new Exception("Cannot download book cover, writer isn't finishing book fast enough.");

            var response = await httpClient
               .GetAsync(bookCoverUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var bookCover = JsonConvert.DeserializeObject<BookCover>(await response.Content.ReadAsStringAsync());

                return bookCover;
            }

            cancellationTokenSource.Cancel();

            return null;
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await booksContext.SaveChangesAsync() > 0);
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (booksContext != null)
                {
                    booksContext.Dispose();
                    booksContext = null;
                }

                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }

            disposed = true;
        }
    }
}
