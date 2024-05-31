using APBD9.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APBD9.Models;

namespace APBD9.Controllers
{
    [ApiController]
    [Route("/api/trips")]
    public class TripsController : ControllerBase
    {
        private readonly ApbdContext _context;

        public TripsController(ApbdContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page <= 0 || pageSize <= 0)
            {
                return BadRequest("Page and Page Size must be positive numbers");
            }

            var totalTrips = await _context.Trips.CountAsync();

            var trips = await _context.Trips
                .OrderByDescending(e => e.DateFrom)
                .Skip(page - 1)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Name,
                    e.Description,
                    e.DateFrom,
                    e.DateTo,
                    e.MaxPeople,
                    Countries = _context.Countries
                        .Where(c => c.IdTrips.Any(t => t.IdTrip == e.IdTrip))
                        .Select(c => new { c.Name })
                        .ToList(),
                    Clients = _context.ClientTrips
                        .Where(ct => ct.IdTrip == e.IdTrip)
                        .Select(ct => new { ct.IdClientNavigation.FirstName, ct.IdClientNavigation.LastName })
                        .ToList()
                })
                .ToListAsync();

            var response = new
            {
                pageNum = page,
                pageSize = pageSize,
                allPages = totalTrips,
                trips = trips
            };

            return Ok(response);
        }



        [HttpDelete("{idClient}")]
        public async Task<IActionResult> DeleteClient(int idClient)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.IdClient == idClient);

            if (client == null)
            {
                return NotFound(new { message = "Client not found." });
            }
            
            if (_context.ClientTrips.Where(e => e.IdClient == client.IdClient).Any())
            {
                return BadRequest(new { message = "Client has assigned trips and cannot be deleted." });
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return Ok("Removed client");
        }
        
[HttpPost("{idTrip}/clients")]
        public async Task<IActionResult> AddClientToTrip(int idTrip, [FromBody] ClientDTO client)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string firstName = client.FirstName;
                string lastName = client.LastName;
                string email = client.Email;
                string telephone = client.Telephone;
                string pesel = client.Pesel;
                DateTime? paymentDate = client.PaymentDate;

                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.Pesel == pesel);

                if (existingClient != null)
                {
                    return BadRequest(new { message = "Client with the given PESEL already exists" });
                }

                var trip = await _context.Trips.FirstOrDefaultAsync(t => t.IdTrip == idTrip);

                if (trip == null)
                {
                    return NotFound(new { message = "Trip not found" });
                }

                if (trip.DateFrom <= DateTime.Now)
                {
                    return BadRequest(new { message = "Cannot register for a trip that has already started" });
                }

                var newClient = new Client
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    Telephone = telephone,
                    Pesel = pesel
                };

                _context.Clients.Add(newClient);
                await _context.SaveChangesAsync();

                var newClientTrip = new ClientTrip
                {
                    IdClient = newClient.IdClient,
                    IdTrip = idTrip,
                    PaymentDate = paymentDate,
                    RegisteredAt = DateTime.Now
                };

                _context.ClientTrips.Add(newClientTrip);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { message = "Client added successfully and registered for the trip" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred while adding the client and/or registering for the trip - rolled back the whole thing");
            }
        }

    }
    
}
