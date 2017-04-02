using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyCodeCamp.Api.Filters;
using MyCodeCamp.Api.Models;
using MyCodeCamp.Data.Entities;
using MyCodeCamp.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyCodeCamp.Api.Controllers
{
   
    [Produces("application/json")]
    [Authorize]
    [EnableCors("AnyGET")]
    [Route("api/camps")]
    [ValidateModel]
    public class CampsController : BaseController
    {
        private readonly ICampRepository _repo;
        private readonly ILogger _looger;
        private readonly IMapper _mapper;

        public CampsController(ICampRepository repo, 
            ILogger<CampsController> logger,
            IMapper mapper)
        {
            _repo = repo;
            _looger = logger;
            _mapper = mapper;
        }

        [HttpGet("")]
        public IActionResult Get()
        {
            var camps = _repo.GetAllCamps();

            return Ok(_mapper.Map<IEnumerable<CampModel>>(camps));
        }

        [HttpGet("{moniker}", Name = "CampGet")]
        public IActionResult Get(string moniker, bool includeSpeakers = false)
        {
            try
            {
                Camp camp = null;

                if (includeSpeakers) camp = _repo.GetCampByMonikerWithSpeakers(moniker);
                else camp = _repo.GetCampByMoniker(moniker);

                if (camp == null) return NotFound($"Camp {moniker} was not found");                

                return Ok(_mapper.Map<CampModel>(camp, opt => opt.Items["UrlHelper"] = this.Url));
            }
            catch
            {}
            return BadRequest();

        }

        [EnableCors("Sdias")]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]CampModel model)
        {
            try
            {
                _looger.LogInformation("Creating a new Code Camp");

                var camp = _mapper.Map<Camp>(model);

                _repo.Add(camp);

                if (await _repo.SaveAllAsync())
                {
                    var newUri = Url.Link("CampGet", new { moniker = camp.Moniker });
                    return Created(newUri, _mapper.Map<CampModel>(camp));
                }
                else
                {
                    _looger.LogWarning("could not saved Camp to the Database");
                }
            }
            catch (Exception ex)
            {

                _looger.LogError($"Threw exception while saving Camp {ex}" );
            }

            return BadRequest();
        }

        [HttpPut("{moniker}")]
        public async Task<IActionResult> Put(string moniker, [FromBody] CampModel model)
        {
            try
            {
                var oldCamp = _repo.GetCampByMoniker(moniker);
                if (oldCamp == null) return NotFound($"Could not find Camp with ID {moniker}");

                _mapper.Map(model, oldCamp);

                if (await _repo.SaveAllAsync())
                {
                    return Ok(_mapper.Map<CampModel>(oldCamp));
                }
            }
            catch (Exception)
            {
                throw;
            }

            return BadRequest("Couldn't update Camp");
        }

        [HttpDelete("{moniker}")]
        public async Task<IActionResult> Delete(string moniker)
        {
            try
            {
                var oldCamp = _repo.GetCampByMoniker(moniker);
                if (oldCamp == null) return NotFound($"Could not find Camp with ID of {moniker}");

                _repo.Delete(oldCamp);
                if ( await _repo.SaveAllAsync())
                {
                    return Ok();
                }
            }
            catch (Exception)
            {

                throw;
            }
            return BadRequest("Could not delete Camp");
        }
    }
}