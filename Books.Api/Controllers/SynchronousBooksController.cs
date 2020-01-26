using Books.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Books.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SynchronousBooksController : ControllerBase
    {
        private readonly IBooksRepository booksRepository;

        public SynchronousBooksController(IBooksRepository booksRepository)
        {
            this.booksRepository = booksRepository ?? throw new ArgumentNullException(nameof(booksRepository));
        }

        [HttpGet]
        public IActionResult GetBooks()
        {
            var bookEntities = booksRepository.GetBooks();

            return Ok(bookEntities);
        }
    }
}