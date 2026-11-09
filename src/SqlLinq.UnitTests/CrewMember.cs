namespace SqlLinq.UnitTests
{
    sealed class CrewMember
    {
        public string Name { get; set; }

        public string Species { get; set; }

        public int Rank { get; set; }

        public int Age { get; set; }

        public decimal Pay { get; set; }

        public int? ShipId { get; set; }
    }
}
