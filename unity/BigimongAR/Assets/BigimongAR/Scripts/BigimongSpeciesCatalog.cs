namespace Bigimong.AR
{
    public readonly struct BigimongSpecies
    {
        public int ArtId { get; }
        public string KoreanName { get; }
        public string GrowthStage { get; }
        public string Grade { get; }

        public BigimongSpecies(int artId, string koreanName, string growthStage, string grade)
        {
            ArtId = artId;
            KoreanName = koreanName;
            GrowthStage = growthStage;
            Grade = grade;
        }
    }

    public static class BigimongSpeciesCatalog
    {
        public const int Count = 30;

        private static readonly string[] KoreanNames =
        {
            "티라노사우루스", "트리케라톱스", "익룡", "스테고사우루스", "브라키오사우루스",
            "스피노사우루스", "안킬로사우루스", "벨로키랍토르", "코리토사우루스", "카르노타우루스",
            "모사사우루스", "알로사우루스", "케찰코아틀루스", "데이노니쿠스", "유오플로케팔루스",
            "바리오닉스", "오비랍토르", "프로토케라톱스", "갈리미무스", "드라코렉스",
            "기가노토사우루스", "딜로포사우루스", "이구아노돈", "켄트로사우루스", "테리지노사우루스",
            "콤프소그나투스", "파라사우롤로푸스", "미크로랍토르", "브론토사우루스", "티타노사우루스"
        };

        public static BigimongSpecies Resolve(int artId)
        {
            var normalized = UnityEngine.Mathf.Clamp(artId, 1, Count);
            // 기본 등급 species are all supplied by the durable v0.19 catalog.
            return new BigimongSpecies(normalized, KoreanNames[normalized - 1], "아동기", "기본");
        }
    }
}
