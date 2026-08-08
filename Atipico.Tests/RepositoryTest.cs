using Moq;
using Atipico.Infraestructure.Persistence.Repositories;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Tests
{
    public class RepositoryTest
    {
        [Fact]
        public void Add_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass();

            var context = new Mock<AppDbContext>();
            var dbSetMock = new Mock<DbSet<TestClass>>(); 

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.Add(It.IsAny<TestClass>())).Returns(testObject);

            var repository = new Repository<TestClass>(context.Object);

            repository.Add(testObject);

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.Add(It.Is<TestClass>(t => t == testObject)));
        }

        [Fact]
        public void Remove_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass();

            var context = new Mock<AppDbContext>();
            var dbSetMock = new Mock<DbSet<TestClass>>();

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.Remove(It.IsAny<TestClass>())).Returns(testObject);

            var repository = new Repository<TestClass>(context.Object);

            repository.Remove(testObject);

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.Remove(It.Is<TestClass>(t => t == testObject)));
        }

        [Fact]
        public void Get_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass();

            var context = new Mock<DbContext>();
            var dbSetMock = new Mock<DbSet<TestClass>>();

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.Find(It.IsAny<int>())).Returns(testObject);

            var repository = new Repository<TestClass>(context.Object);
            var result = repository.GetAllAsync();

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.Find(It.Is<int>(id => id == 1)));

            Assert.Equal((IEnumerable<T>?)testObject, result);

        }

        [Fact]
        public void GetAll_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass() { id = 1 };
            var testList = new List<TestClass> { testObject };

            var dbSetMock = new Mock<DbSet<TestClass>>();
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.Provider).Returns(testList.AsQueryable().Provider);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.Expression).Returns(testList.AsQueryable().Expression);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.ElementType).Returns(testList.AsQueryable().ElementType);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.GetEnumerator()).Returns(testList.AsQueryable().GetEnumerator());

            var context = new Mock<AppDbContext>();
            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);

            var repository = new Repository<TestClass>(context.Object);

            var result = repository.GetAllAsync();

            Assert.Equal(testList, result.Result.ToList());
        }

        [Fact]
        public void Find_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass() { id = 1 };
            var testList = new List<TestClass> { testObject };

            var dbSetMock = new Mock<DbSet<TestClass>>();
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.Provider).Returns(testList.AsQueryable().Provider);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.Expression).Returns(testList.AsQueryable().Expression);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.ElementType).Returns(testList.AsQueryable().ElementType);
            dbSetMock.As<IQueryable<TestClass>>().Setup(m => m.GetEnumerator()).Returns(testList.AsQueryable().GetEnumerator());

            var context = new Mock<AppDbContext>();
            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);

            var repository = new Repository<TestClass>(context.Object);

            var result = repository.FindAsync(t => t.id == 1);

            Assert.Equal(testList, result.Result.ToList());
        }
    }
}