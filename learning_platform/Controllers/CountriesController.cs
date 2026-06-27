using DataAccess.Data;
using DataAccess.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/countries")]
    [AllowAnonymous]
    public class CountriesController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<CountryDto>>> GetCountries()
        {
            var countries = await context.Countries
                .AsNoTracking()
                .OrderBy(c => c.name)
                .Select(c => new CountryDto
                {
                    CountryId = c.country_id,
                    Name = c.name,
                    IsoCode = c.iso_code
                })
                .ToListAsync();

            return Ok(countries);
        }
    }
}
