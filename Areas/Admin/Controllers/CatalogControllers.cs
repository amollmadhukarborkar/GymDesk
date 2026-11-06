namespace GymDesk.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class PlansController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index(int? id)
    {
        ViewBag.Editing = id is null ? null : await db.MembershipPlans.FirstOrDefaultAsync(plan => plan.Id == id);
        return View(await db.MembershipPlans.OrderBy(plan => plan.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Save(PlanInput input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Check the plan fields and try again.");
            return RedirectToAction(nameof(Index), new { id = input.Id == 0 ? (int?)null : input.Id });
        }

        var plan = input.Id == 0
            ? new MembershipPlan()
            : await db.MembershipPlans.FirstOrDefaultAsync(item => item.Id == input.Id);
        if (plan is null)
        {
            return NotFound();
        }

        plan.Name = input.Name.Trim();
        plan.ValidDays = input.ValidDays;
        plan.VisitAllowance = input.VisitAllowance is > 0 ? input.VisitAllowance : null;
        plan.Price = input.Price;
        plan.IsActive = input.IsActive;
        if (input.Id == 0)
        {
            db.MembershipPlans.Add(plan);
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync($"Saved membership plan {plan.Name}.", "info");
        Note("Plan saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.MembershipPlans.FirstOrDefaultAsync(item => item.Id == id);
        if (plan is not null)
        {
            db.MembershipPlans.Remove(plan);
            await db.SaveChangesAsync();
            await activity.WriteAsync($"Removed plan {plan.Name}.", "warning");
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class ProductsController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index(int? id)
    {
        ViewBag.Editing = id is null ? null : await db.Products.FirstOrDefaultAsync(product => product.Id == id);
        return View(await db.Products.OrderBy(product => product.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Save(ProductInput input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Check the product fields and try again.");
            return RedirectToAction(nameof(Index));
        }

        if (await db.Products.AnyAsync(product => product.Barcode == input.Barcode && product.Id != input.Id))
        {
            Warn("That barcode is already in use.");
            return RedirectToAction(nameof(Index), new { id = input.Id == 0 ? (int?)null : input.Id });
        }

        var product = input.Id == 0
            ? new Product { CreatedAt = DateTime.UtcNow }
            : await db.Products.FirstOrDefaultAsync(item => item.Id == input.Id);
        if (product is null)
        {
            return NotFound();
        }

        product.Name = input.Name.Trim();
        product.Description = input.Description.Trim();
        product.Price = input.Price;
        product.StockQuantity = input.StockQuantity;
        product.Barcode = input.Barcode.Trim();
        if (input.Id == 0)
        {
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync($"Saved product {product.Name}. Stock is {product.StockQuantity}.", "info");
        Note("Product saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FirstOrDefaultAsync(item => item.Id == id);
        if (product is not null)
        {
            db.Products.Remove(product);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class TrainersController(GymDbContext db, UploadStore uploads, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index(int? id)
    {
        ViewBag.Editing = id is null ? null : await db.Trainers.FirstOrDefaultAsync(trainer => trainer.Id == id);
        return View(await db.Trainers.OrderBy(trainer => trainer.FullName).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Save(TrainerInput input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Check the trainer fields and try again.");
            return RedirectToAction(nameof(Index));
        }

        var trainer = input.Id == 0
            ? new Trainer()
            : await db.Trainers.FirstOrDefaultAsync(item => item.Id == input.Id);
        if (trainer is null)
        {
            return NotFound();
        }

        trainer.FullName = input.FullName.Trim();
        trainer.Biography = input.Biography.Trim();
        trainer.HourlyRate = input.HourlyRate;
        trainer.TenSessionRate = input.TenSessionRate;
        if (input.Photo is { Length: > 0 })
        {
            try
            {
                trainer.PhotoPath = await uploads.SaveImageAsync(input.Photo, "trainers");
            }
            catch (InvalidOperationException exception)
            {
                Warn(exception.Message);
                return RedirectToAction(nameof(Index), new { id = input.Id == 0 ? (int?)null : input.Id });
            }
        }

        if (input.Id == 0)
        {
            db.Trainers.Add(trainer);
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync($"Saved trainer {trainer.FullName}.", "info");
        Note("Trainer saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var trainer = await db.Trainers.FirstOrDefaultAsync(item => item.Id == id);
        if (trainer is not null)
        {
            db.Trainers.Remove(trainer);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class ScheduleController(GymDbContext db) : DeskController
{
    public async Task<IActionResult> Index(int? id)
    {
        ViewBag.Editing = id is null ? null : await db.ClassSlots.FirstOrDefaultAsync(slot => slot.Id == id);
        return View(await db.ClassSlots.OrderBy(slot => slot.DayName).ThenBy(slot => slot.StartsAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Save(ClassSlotInput input)
    {
        if (!ModelState.IsValid || input.EndsAt <= input.StartsAt || !WeekDays.Names.Contains(input.DayName))
        {
            Warn("Give the class a name, a weekday, and an end time after the start time.");
            return RedirectToAction(nameof(Index));
        }

        var slot = input.Id == 0
            ? new ClassSlot()
            : await db.ClassSlots.FirstOrDefaultAsync(item => item.Id == input.Id);
        if (slot is null)
        {
            return NotFound();
        }

        slot.Title = input.Title.Trim();
        slot.DayName = input.DayName;
        slot.StartsAt = input.StartsAt;
        slot.EndsAt = input.EndsAt;
        slot.ColorHex = input.ColorHex;
        if (input.Id == 0)
        {
            db.ClassSlots.Add(slot);
        }

        await db.SaveChangesAsync();
        Note("Class saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var slot = await db.ClassSlots.FirstOrDefaultAsync(item => item.Id == id);
        if (slot is not null)
        {
            db.ClassSlots.Remove(slot);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
