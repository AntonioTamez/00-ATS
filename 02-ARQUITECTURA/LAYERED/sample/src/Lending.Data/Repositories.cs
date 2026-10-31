using Microsoft.Extensions.DependencyInjection;

namespace Lending.Data;

/// <summary>An in-memory stand-in for the database: three tables and a lock.</summary>
public sealed class LendingDatabase
{
    internal object Gate { get; } = new();
    internal List<BookRecord> Books { get; } = [];
    internal List<MemberRecord> Members { get; } = [];
    internal List<LoanRecord> Loans { get; } = [];
    internal int NextId(int current) => current + 1;
}

// Repositories hand out COPIES, like a database does: changing a record you read changes nothing until you Update it.

public sealed class BookRepository(LendingDatabase db)
{
    public IReadOnlyList<BookRecord> GetAll()
    {
        lock (db.Gate) return db.Books.Select(b => b.Copy()).ToList();
    }

    public BookRecord? GetById(int id)
    {
        lock (db.Gate) return db.Books.FirstOrDefault(b => b.Id == id)?.Copy();
    }

    public BookRecord Insert(BookRecord book)
    {
        lock (db.Gate)
        {
            var row = book.Copy();
            row.Id = db.Books.Count == 0 ? 1 : db.Books.Max(b => b.Id) + 1;
            db.Books.Add(row);
            return row.Copy();
        }
    }
}

public sealed class MemberRepository(LendingDatabase db)
{
    public MemberRecord? GetById(int id)
    {
        lock (db.Gate) return db.Members.FirstOrDefault(m => m.Id == id)?.Copy();
    }

    public MemberRecord? GetByEmail(string email)
    {
        lock (db.Gate) return db.Members.FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase))?.Copy();
    }

    public MemberRecord Insert(MemberRecord member)
    {
        lock (db.Gate)
        {
            var row = member.Copy();
            row.Id = db.Members.Count == 0 ? 1 : db.Members.Max(m => m.Id) + 1;
            db.Members.Add(row);
            return row.Copy();
        }
    }
}

public sealed class LoanRepository(LendingDatabase db)
{
    public LoanRecord? GetById(int id)
    {
        lock (db.Gate) return db.Loans.FirstOrDefault(l => l.Id == id)?.Copy();
    }

    public IReadOnlyList<LoanRecord> GetByMember(int memberId)
    {
        lock (db.Gate) return db.Loans.Where(l => l.MemberId == memberId).Select(l => l.Copy()).ToList();
    }

    public IReadOnlyList<LoanRecord> GetActiveByMember(int memberId)
    {
        lock (db.Gate) return db.Loans.Where(l => l.MemberId == memberId && l.IsActive).Select(l => l.Copy()).ToList();
    }

    public LoanRecord? GetActiveByBook(int bookId)
    {
        lock (db.Gate) return db.Loans.FirstOrDefault(l => l.BookId == bookId && l.IsActive)?.Copy();
    }

    public IReadOnlySet<int> GetBorrowedBookIds()
    {
        lock (db.Gate) return db.Loans.Where(l => l.IsActive).Select(l => l.BookId).ToHashSet();
    }

    public LoanRecord Insert(LoanRecord loan)
    {
        lock (db.Gate)
        {
            var row = loan.Copy();
            row.Id = db.Loans.Count == 0 ? 1 : db.Loans.Max(l => l.Id) + 1;
            db.Loans.Add(row);
            return row.Copy();
        }
    }

    public void Update(LoanRecord loan)
    {
        lock (db.Gate)
        {
            var index = db.Loans.FindIndex(l => l.Id == loan.Id);
            if (index < 0) throw new InvalidOperationException($"Loan {loan.Id} does not exist.");
            db.Loans[index] = loan.Copy();
        }
    }
}

public static class DataRegistration
{
    public static IServiceCollection AddLendingData(this IServiceCollection services)
    {
        services.AddSingleton<LendingDatabase>();
        services.AddScoped<BookRepository>();
        services.AddScoped<MemberRepository>();
        services.AddScoped<LoanRepository>();
        return services;
    }
}
