using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Xunit;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The column widths and list bounds of the user log, and the fact that the enum values are
    /// schema.
    /// </summary>
    public sealed class UserLogListLimitsTests
    {
        [Fact]
        public void TheColumnWidthsMirrorTheConnectionLog()
        {
            Assert.Equal(45, UserLogListLimits.IpMaxLength);
            Assert.Equal(512, UserLogListLimits.UserAgentMaxLength);
            Assert.Equal(2, UserLogListLimits.CountryCodeLength);
            Assert.Equal(255, UserLogListLimits.IspMaxLength);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, UserLogListLimits.DefaultPageSize);
            Assert.Equal(1000, UserLogListLimits.MaxPageSize);
            Assert.Equal(200, UserLogListLimits.MaxSearchLength);
            Assert.Equal(100, UserLogListLimits.MaxFilterValues);
        }

        [Fact]
        public void TheDescriptionBoundsAreTheContractTheWritePathReliesOn()
        {
            Assert.Equal(500, UserLogDescriptions.DescriptionMaxLength);
            Assert.Equal(64, UserLogDescriptions.SubjectMaxLength);
            Assert.Equal("(empty)", UserLogDescriptions.EmptySubject);
        }

        [Theory]
        [InlineData(UserLogSortField.CreatedAt, 0)]
        [InlineData(UserLogSortField.RecordType, 1)]
        public void TheSortFieldNumbersAreTheSchema(UserLogSortField field, int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(UserLogSortOrder.Ascending, 0)]
        [InlineData(UserLogSortOrder.Descending, 1)]
        public void TheSortOrderNumbersAreTheSchema(UserLogSortOrder order, int stored)
        {
            Assert.Equal(stored, (int)order);
        }

        [Theory]
        [InlineData(UserLogRecordType.RegistrationSubmit, 0)]
        [InlineData(UserLogRecordType.RegistrationConfirm, 1)]
        [InlineData(UserLogRecordType.Login, 2)]
        [InlineData(UserLogRecordType.PasswordResetRequest, 3)]
        [InlineData(UserLogRecordType.PasswordResetConfirm, 4)]
        [InlineData(UserLogRecordType.UsernameChange, 5)]
        [InlineData(UserLogRecordType.EmailChange, 6)]
        [InlineData(UserLogRecordType.PasswordChange, 7)]
        [InlineData(UserLogRecordType.LoginGuardChange, 8)]
        [InlineData(UserLogRecordType.Logout, 9)]
        public void TheRecordTypeNumbersAreTheSchema(UserLogRecordType recordType, int stored)
        {
            Assert.Equal(stored, (int)recordType);
        }
    }
}
