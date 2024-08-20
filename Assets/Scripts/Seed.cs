public class Seed {
    private long value;

    public Seed(long seed){
        value = seed;
    }

    public void Increment(){
        value++;
    }

    public static implicit operator long(Seed seed){
        return seed.value;
    }
}