using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Books.Api.Filters;
using Books.Api.Models;
using Books.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Books.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly IBooksRepository booksRepository;
        private readonly IMapper mapper;

        public BooksController(IBooksRepository booksRepository, IMapper mapper)
        {
            this.booksRepository = booksRepository ?? throw new ArgumentNullException(nameof(booksRepository));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet]
        [BooksResultFilter]
        public async Task<IActionResult> GetBooks()
        {
            var bookEntities = await booksRepository.GetBooksAsync();

            return Ok(bookEntities);
        }

        [HttpGet]
        [Route("{id}", Name = "GetBook")]
        //[BookResultFilter]
        [BookWithCoversResultFilter]
        public async Task<IActionResult> GetBook(Guid id)
        {
            var bookEntity = await booksRepository.GetBookAsync(id);

            if (bookEntity == null)
            {
                return NotFound();
            }

            //var bookCover = await booksRepository.GetBookCoverAsync("dummyCover");
            var bookCovers = await booksRepository.GetBookCoversAsync(id);

            return Ok((bookEntity, bookCovers));
        }

        [HttpPost]
        [BookResultFilter]
        public async Task<IActionResult> CreateBook([FromBody] BookForCreation book)
        {
            var bookEntity = mapper.Map<Entities.Book>(book);
            booksRepository.AddBook(bookEntity);

            await booksRepository.SaveChangesAsync();

            // Fetch (refetch) the book from the data store, including the author
            await booksRepository.GetBookAsync(bookEntity.Id);

            return CreatedAtRoute("GetBook", new { id = bookEntity.Id }, bookEntity);
        }
    }
}