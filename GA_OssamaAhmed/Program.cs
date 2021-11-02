
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.VisualBasic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics;
namespace GA_OssamaAhmed
{
    static class Program  // why class has to be static or internal to avoid error  //Compiler Error CS1106
    {
        static void Main(string[] args)
        {
            #region main class
            hyperparameters hp = new hyperparameters { popSize = 10, nGenerations = 200, mutationRate = 0.2 };
            options op = new options()
            {
                selection = SelectionOperator.RoulleteWheel,
                doublePointOperator = CrossoverOperator.doublePointOperator,
                mutOperator = MutationOperator.Swap,
                nElites = ElitesNum.two,
                fit = Fitness.d
            };
            City c1 = new City() { x = 5, y = 5, name = "1" };
            City c2 = new City() { x = 10, y = 10, name = "2" };
            City c3 = new City() { x = 20, y = 20, name = "3" };
            City c4 = new City() { x = 1, y = 10, name = "4" };
            City c5 = new City() { x = 4, y = 3, name = "5" };
            City c6 = new City() { x = 35, y = 45, name = "6" };
            City c7 = new City() { x = 16, y = 13, name = "7" };
            City c8 = new City() { x = 4, y = 35, name = "8" };
            City c9 = new City() { x = 9, y = 19, name = "9" };
            City c10 = new City() { x = 7, y = 47, name = "10" };
            
            List<City> cities = new List<City>() { c1, c2, c3, c4, c5, c6, c7, c8, c9, c10 };
            

            //ConcurrentBag<Chromosome> ch = new ConcurrentBag<Chromosome>();

            
            createNewGen cr = new createNewGen();
            var selectionOperator =cr.SelectFuncs.Where(x => x.selectOP == op.selection).FirstOrDefault().selection; //delegeate for opeartors

            var crossoverOperator = cr.CandidateFuncs.Where(x => x.crossOP == op.doublePointOperator).FirstOrDefault().cvCandidates;//delegeate for opeartors

            var mutationOperator = cr.mutationFuncs.Where(x => x.mutationOP == op.mutOperator).FirstOrDefault().mutation;//delegeate for opeartors

            Generation initialGen = initGen(cities, hp); // intial gen
            Generation evalg = FitnessGen(initialGen,FitnessChromosome); //evaluate gen
            Stopwatch sw = new Stopwatch();
            sw.Start();

            int i = 0;
            var oldG = evalg;

            while (i < hp.nGenerations)
            {
                #region selection of elites

                ConcurrentBag<Chromosome> elitesList = new ConcurrentBag<Chromosome>();
                var ordered = oldG.chrom_list.OrderByDescending(x => x.fitness_score).Take(2);
                foreach ( var x in ordered)
                {
                    elitesList.Add(x);
                }
                


                #endregion




                #region selection , crossover,mutation
                BufferBlock<Generation> bufferBlockGenSelection = new BufferBlock<Generation>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 4
            });

            TransformBlock<Generation, (Chromosome, Chromosome)> selectionCandidates = new TransformBlock<Generation, (Chromosome, Chromosome)>
                (x =>
           {

                var ch1 = selectionOperator(x);
               var ch2 = selectionOperator(x);
               while (ch1.route.SequenceEqual(ch2.route))  // to make sure chromosomes are different from each other
                {
                   ch2 = selectionOperator(x);
               }

               return (ch1, ch2);
           }
            , new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 4
            });

            TransformBlock<(Chromosome, Chromosome), (Chromosome, Chromosome)> crossover = new TransformBlock<(Chromosome, Chromosome), (Chromosome, Chromosome)>
                (x => 
                {
                    return crossoverOperator((x.Item1, x.Item2));

                },new ExecutionDataflowBlockOptions {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 4
                });


            TransformBlock<(Chromosome, Chromosome), (Chromosome, Chromosome)> mutation = new TransformBlock<(Chromosome, Chromosome), (Chromosome, Chromosome)>
                (x =>
                {
                    var child1 = mutationOperator(x.Item1, hp);
                    var child2 = mutationOperator(x.Item2, hp);
                    return (child1,child2);

                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 4
                });


            Generation afterMutationGen = new Generation();
            ActionBlock<(Chromosome,Chromosome)> action2 = new ActionBlock<(Chromosome,Chromosome)>(x => {

                afterMutationGen.chrom_list.Add(x.Item1);
                afterMutationGen.chrom_list.Add( x.Item2);

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 4
            });


            bufferBlockGenSelection.LinkTo(selectionCandidates, new DataflowLinkOptions() { PropagateCompletion = true });

            selectionCandidates.LinkTo(crossover, new DataflowLinkOptions() { PropagateCompletion = true });

            crossover.LinkTo(mutation, new DataflowLinkOptions() { PropagateCompletion = true });

            crossover.LinkTo(action2, new DataflowLinkOptions() { PropagateCompletion = true });

            var size = initialGen.chrom_list.Count ;

            for (int k = 0 ; k<size-2;k++)  // why does this have to be size - 2 to get right number of chromosomes!
            {
                    bufferBlockGenSelection.SendAsync(oldG).Wait();
            }

            bufferBlockGenSelection.Complete(); 

            Task.WaitAll(action2.Completion);
                #endregion
                //foreach (var a in afterMutationGen.chrom_list)
                //{
                //    Console.WriteLine();
                //    Console.WriteLine("aftermutation generation");

                //    a.displayRoute();
                //    //Console.WriteLine(a.fitness_score + "is the fitness score after mutation");
                //}
                Console.WriteLine("size of generation after mutation" + afterMutationGen.chrom_list.Count);


                Generation elitedGen = new Generation() { chrom_list = afterMutationGen.chrom_list };

                foreach(var x in elitesList) {
                    elitedGen.chrom_list.Add(x);    // add elites to final generation after mutation
                }

                //elitesList.ForEach(x => elitedGen.chrom_list.Add(x));  // add elites to final generation after mutation


                Console.WriteLine("size of generation after adding elites" + elitedGen.chrom_list.Count);

                #region Evaluation 

                BufferBlock<Chromosome> bufferBlock1 = new BufferBlock<Chromosome>(new ExecutionDataflowBlockOptions  // buffer for evaluation
                {
                    MaxDegreeOfParallelism = 1,
                    BoundedCapacity = 4
                });

                TransformBlock<Chromosome, Chromosome> evaluateGen = new TransformBlock<Chromosome, Chromosome>(x =>
                {

                    var ch = FitnessChromosome(x);

                    return ch;

                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 4
                });

                Generation EvalGen = new Generation();  // evaluated gen
                
                ActionBlock<Chromosome> actionBlock = new ActionBlock<Chromosome>(x => {


                    EvalGen.chrom_list.Add(x);
                    //Console.WriteLine("fitness score for chromosome  = " + x.fitness_score);
                    //x.displayRoute();
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 4
                });

                bufferBlock1.LinkTo(evaluateGen, new DataflowLinkOptions() { PropagateCompletion = true });   // flow for evaluation
                evaluateGen.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });   // flow for evaluation



                foreach (var c in elitedGen.chrom_list)
                {
                    bufferBlock1.SendAsync(c).Wait();
                }

                bufferBlock1.Complete(); // without this and the next line the program exits without finishing the action block why?

                Task.WaitAll(actionBlock.Completion);  // without this and the next line the program exits without finishing the action block why ?


                #endregion


                var solution = EvalGen.chrom_list.OrderByDescending(x => x.fitness_score).FirstOrDefault();
                Console.WriteLine("generation number " +i);
                Console.WriteLine("this generation best solution is : ");
                Console.WriteLine("fitness score is " + solution.fitness_score +"and distance is " + 1/ solution.fitness_score);
                solution.displayRoute();


                oldG = EvalGen;
                i++;



            }




            sw.Stop();
            Console.WriteLine("Elapsed={0}", sw.Elapsed);


            #endregion
        }


        public class ThreadSafeRandom    // for use in paralell thread
        {
            private static readonly Random _global = new Random();
            [ThreadStatic] private static Random _local;

            public int Next()
            {
                if (_local == null)
                {
                    int seed;
                    lock (_global)
                    {
                        seed = _global.Next();
                    }
                    _local = new Random(seed);
                }

                return _local.Next();
            }

            public int Next(int low, int high)
            {
                if (_local == null)
                {
                    int seed;
                    lock (_global)
                    {
                        seed = _global.Next();
                    }
                    _local = new Random(seed);
                }

                return _local.Next(low, high);
            }
            public double NextDouble()
            {
                if (_local == null)
                {
                    int seed;
                    lock (_global)
                    {
                        seed = _global.Next();
                    }
                    _local = new Random(seed);
                }

                return _local.NextDouble();
            }
        }



        #region genetic algorithm Classes and Functions
        public class createNewGen
        {
            public List<(SelectionOperator selectOP, Func<Generation, Chromosome> selection)> SelectFuncs;

            public List<(CrossoverOperator crossOP, Func<(Chromosome, Chromosome), (Chromosome, Chromosome)> cvCandidates)> CandidateFuncs;

            public List<(MutationOperator mutationOP, Func<Chromosome, hyperparameters, Chromosome> mutation)> mutationFuncs;

            public List<(ElitesNum n, Func<Generation, Generation> SelectElites)> SelectelitesFuncs;

            public List<(ElitesNum n, Func<Generation, Generation> AddtElites)> AddelitesFuncs;

            public List<(Fitness f, Func<Chromosome, Chromosome> fitnessChrom)> fitnessFuncs;

            public createNewGen() // intilize gen with different operators
            {
                SelectFuncs = new List<(SelectionOperator selectOP, Func<Generation, Chromosome> selection)>(){
                (SelectionOperator.RoulleteWheel ,  RoulleteSelection)
            };

                CandidateFuncs = new List<(CrossoverOperator crossOP, Func<(Chromosome, Chromosome), (Chromosome, Chromosome)> cvCandidates)>()
            {
                (CrossoverOperator.doublePointOperator , TwoPointsCrossover)
            };

                mutationFuncs = new List<(MutationOperator mutationOP, Func<Chromosome, hyperparameters, Chromosome> mutation)>()
                {
                    (MutationOperator.Swap , Swap)
                };


                SelectelitesFuncs = new List<(ElitesNum n, Func<Generation, Generation> SelectElites)>()
                {
                    (ElitesNum.two , SelectElites2)
                };
                AddelitesFuncs = new List<(ElitesNum n, Func<Generation, Generation> AddtElites)>()
                {
                    (ElitesNum.two , addElites2)
                };

                fitnessFuncs = new List<(Fitness f, Func<Chromosome, Chromosome> fitnessChrom)>()
                {
                    (Fitness.d , FitnessChromosome)
                };
            }

        }



        // pipeline looks like input gen>>evaluation fitness>>select elites >> selection candidates>> crossover >> muation >> add past elites>> output gen
        public static Func<Generation, Generation> CreateGenPipeLine(createNewGen cr, options op, hyperparameters hp)
        {

            Console.WriteLine("started building new generation");
            Func<Generation, Generation> selectElites = cr.SelectelitesFuncs.Where(x => x.n == ElitesNum.two).FirstOrDefault().SelectElites; // select elites func before start of selection to add later in 

            Func<Generation, Chromosome> selectionFunc = cr.SelectFuncs.Where(x => x.selectOP == SelectionOperator.RoulleteWheel).FirstOrDefault().selection; // selection function based on options

            Func<Generation, (CrossoverCandidates, ConcurrentBag<Chromosome>)> selectCandidates = (x) => Candidates(x, hp, op, selectionFunc); // selection of overall candidates

            var crossoverFunc = cr.CandidateFuncs.Where(x => x.crossOP == CrossoverOperator.doublePointOperator).FirstOrDefault().cvCandidates;

            Func<(CrossoverCandidates, ConcurrentBag<Chromosome>), Generation> crossoverGen = (x) => Crossover(x, crossoverFunc);

            Func<Generation, Generation> addElites = cr.AddelitesFuncs.Where(x => x.n == ElitesNum.two).FirstOrDefault().AddtElites;  // add elites func

            var mutationFunc = cr.mutationFuncs.Where(x => x.mutationOP == MutationOperator.Swap).FirstOrDefault().mutation;

            Func<Generation, Generation> mutationGen = (x) => Mutation(x, op, hp, mutationFunc);

            var fitnessFunc = cr.fitnessFuncs.Where(x => x.f == Fitness.d).FirstOrDefault().fitnessChrom;

            Func<Generation, Generation> fitGen = (x) => FitnessGen(x, fitnessFunc);


            //return selectCandidates.Compose(crossoverGen).Compose(mutationGen).Compose(addElites).Compose(selectElites).Compose(fitGen);


            return fitGen.Compose(selectElites).Compose(selectCandidates).Compose(crossoverGen).Compose(mutationGen).Compose(addElites);

        }


        public static Chromosome GeneticAlgoStart(Generation initialG, createNewGen cr, options op, hyperparameters hp,
            Func<createNewGen, options, hyperparameters, Func<Generation, Generation>> CreateGenPipeLine,
            Func<Generation, Func<Chromosome, Chromosome>, Generation> fitGen)
        {

            Func<Generation, Generation> CreateGen = CreateGenPipeLine(cr, op, hp);
            int i = 0;
            Generation oldGen = initialG;
            Generation oldGenEvaluated = fitGen(oldGen, FitnessChromosome);
            //Console.WriteLine($"max fitness for generation {i}");
            //Console.WriteLine(oldGenEvaluated.chrom_list.Max(x => x.fitness_score));
            i += 1;

            Chromosome solution = new Chromosome();
            while (i < hp.nGenerations)
            {
                Console.WriteLine($"max fitness for generation {i}");

                Generation newGen = new Generation();
                newGen = CreateGen(oldGen);
                Generation newGenEval = fitGen(newGen, FitnessChromosome);
                //Console.WriteLine($"max fitness for generation {i}  = {newGenEval.chrom_list.Max(x => x.fitness_score)}");

                solution = newGenEval.chrom_list.OrderByDescending(x => x.fitness_score).FirstOrDefault();
                Console.WriteLine(solution.fitness_score);

                Console.WriteLine($" and its route is ");
                solution.displayRoute();

                oldGen = newGenEval;

                i++;
            }
            Console.WriteLine($"best solution after {hp.nGenerations} generation is {solution.fitness_score} , distance is {1 / solution.fitness_score} and its route is ");
            solution.displayRoute();
            return solution;

        }


        #endregion



        #region Fucntions 


        public static Chromosome FitnessChromosome(Chromosome input)
        {
            static double calc_distance(City c1, City c2)   // defined internally to preserve purity
            {
                double distance = 0;
                double x_distance = c1.x - c2.x;
                double y_distance = c1.y - c2.y;
                distance = Math.Sqrt(Math.Pow(x_distance, 2) + Math.Pow(y_distance, 2));
                return distance;
            }

            Chromosome ch = new Chromosome() { route = input.route, fitness_score = input.fitness_score };
            double total_distance = 0;
            for (int i = 0; i < ch.route.Count - 1; i++)
            {
                total_distance += calc_distance(ch.route[i], ch.route[i + 1]);  // calculate distance from first city to last in route
            }
            total_distance += calc_distance(ch.route.Last(), ch.route.First());  //  add distance from last to first as per condition of tsp 


            ch.fitness_score = 1 / total_distance;

            return ch;
        }

        public static Generation FitnessGen(Generation g, Func<Chromosome, Chromosome> FitnessChromosome)
        {

            Generation newGen = new Generation();
            foreach(var ch  in g.chrom_list)
            {
                var x =FitnessChromosome(ch);
                newGen.chrom_list.Add(x);
            }

             
            return newGen;
        }

        public static Generation initGen(List<City> cities, hyperparameters hp) //initial generation only
        {
            Random rnd = new Random();
            Generation gen = new Generation();
            for (int i = 0; i < hp.popSize; i++) {
                Chromosome ch = new Chromosome();   // why when i intilize chromosome object outside for loop all population solutions are the same!
                ch.route = cities.OrderBy(x => rnd.Next()).ToList();
                gen.chrom_list.Add(ch);
            }

            return gen;
        }



        static double getRandomDouble(double minNum, double maxNum) // used to generate random number between 2 boundries
        {
            double range = maxNum - minNum;

            ThreadSafeRandom rnd = new ThreadSafeRandom();  //Thread safe
            
            //Random rnd = new Random();
            return (rnd.NextDouble() * range) + minNum;
        }


        public static Chromosome RoulleteSelection(Generation g)                   // input has to have fitness scores
        {

            Chromosome solution = new Chromosome();
            double sum_fitness = 0;
            List<double> cum_sum = new List<double>(); // cum sum for each chromosome add it to list
            List<Chromosome> pop = g.chrom_list.OrderByDescending(s => s.fitness_score).ToList();   // order by greater score first

            foreach (Chromosome ch in pop)
            {
                sum_fitness += ch.fitness_score; //  get sum of scores for the roullete wheel 

                cum_sum.Add(sum_fitness);
            }
            double rndNum = getRandomDouble(0, sum_fitness);
            double sol;

            if (cum_sum[0] > rndNum)
            {
                sol = cum_sum[0];
            }
            else { sol = cum_sum.Where(x => x <= rndNum).Max(); }
            int index = cum_sum.IndexOf(sol);

            solution = pop[index];


            return solution;

        }

        public static (CrossoverCandidates, ConcurrentBag<Chromosome>) Candidates(Generation g, hyperparameters hp, options op, Func<Generation, Chromosome> selectionFunc)
        {
            CrossoverCandidates parents = new CrossoverCandidates() { candidates = new List<(Chromosome, Chromosome)>() };  // list of crossover candidates

            int size = (int)((hp.popSize / 2) - 1); // make sure size is int if popsize is odd number  and reserve a spot for elites

            for (int i = 0; i < size; i++)
            {
                var ch1 = selectionFunc(g);
                var ch2 = selectionFunc(g);
                while (ch1.route.SequenceEqual(ch2.route))  // to make sure chromosomes are different from each other
                {
                    ch2 = selectionFunc(g);
                }

                parents.candidates.Add((ch1, ch2));
            }

            return (parents, g.elites);

        }
        // have to sepearte operator from crossover function itself as it can be changed
        public static (Chromosome, Chromosome) TwoPointsCrossover((Chromosome, Chromosome) candidates)
        {
            //Random rnd = new Random();

            ThreadSafeRandom rnd = new ThreadSafeRandom();
            var ch1 = candidates.Item1;// first parent

            var ch2 = candidates.Item2;// second parent 
            Chromosome child1 = new Chromosome();
            Chromosome child2 = new Chromosome();
            int size = ch2.route.Count;
            // in summary i slice each parent into 3 parts take the middle part and replace it with the middle of the other parent
            //only if itdoesn't exist already and shift all other items based on this order
            int n1 = rnd.Next(0, size / 3);
            int n2 = rnd.Next(size / 3, 2 * size / 3);

            var slice1 = ch1.route.Skip(n1).Take(n2).ToList();                // slice 1 start from n1 and range of n2 which will be added to a child
            var slice2 = ch1.route.Where(x => !slice1.Contains(x)).ToList();  //  the rest of  ch1
            var slice3 = ch2.route.Where(x => !slice1.Contains(x)).ToList();  // to make sure the city order is unique in child i filter by slice1 
            var slice4 = ch2.route.Where(x => !slice3.Contains(x)).ToList(); // the rest of ch2

            child1.route = slice1.Concat(slice3).ToList();

            child2.route = slice2.Concat(slice4).ToList();

            return (child1, child2);
        }

        public static Generation Crossover((CrossoverCandidates, ConcurrentBag<Chromosome>) candidatesElitesMix, Func<(Chromosome, Chromosome), (Chromosome, Chromosome)> crossoverFunc)
        {

            Generation g = new Generation();
            //Random rnd = new Random();

            ThreadSafeRandom rnd = new ThreadSafeRandom();   // Thread safe   not used
            var solution_list = g.chrom_list;
            var parents = candidatesElitesMix.Item1.candidates; // candidates list for cross over from candidates object
            foreach (var parentpair in parents)
            {
                var pair = crossoverFunc(parentpair);

                solution_list.Add(pair.Item1);
                solution_list.Add(pair.Item2);

            }

            //save elites in new gen 

            g.elites = candidatesElitesMix.Item2;

            return g;
        }

        public static Chromosome Swap(Chromosome c1, hyperparameters hp) //mutation operator
        {
            Chromosome ch = new Chromosome() { route = c1.route, fitness_score = c1.fitness_score };  // copy chromosome
            //Random rnd = new Random();


            ThreadSafeRandom rnd = new ThreadSafeRandom();
            int size = ch.route.Count;
            if (rnd.NextDouble() > hp.mutationRate)
            {
                int a = rnd.Next(0, size);
                int b = rnd.Next(0, size);
                while (a == b)
                {
                    b = rnd.Next(0, size);  // make sure index a and b are not the same 
                }
                var temp = ch.route[a];    // swap city a and b 
                ch.route[a] = ch.route[b];
                ch.route[b] = temp;
            }

            return ch;
        }


       
        public static Generation Mutation(Generation g, options op, hyperparameters hp, Func<Chromosome, hyperparameters, Chromosome> Swap)
        {

            Generation new_gen = new Generation() { elites = g.elites };
            
            foreach(var x in g.chrom_list)
            {
                var ch =Swap(x, hp);
                new_gen.chrom_list.Add(ch);
            }
            
            //new_gen.chrom_list = g.chrom_list.Select(x => Swap(x, hp)).ToList();

            

            return new_gen;
        }

        public static Generation SelectElites2(Generation g)
        {
            Generation new_gen = new Generation() { chrom_list = g.chrom_list };
            var a = g.chrom_list.OrderByDescending(x => x.fitness_score).Take(2);
            foreach(var x in a)
            {
                new_gen.elites.Add(x);
            } 

            return new_gen;

        }

        public static Generation addElites2(Generation g)  
        {


            Generation new_gen = new Generation() { elites = g.elites, chrom_list = g.chrom_list };
            foreach(var a in new_gen.elites) {
                new_gen.chrom_list.Add(a);
            };
        

            return new_gen;
        }


        public static Func<T1, T3> Compose<T1, T2, T3>(this Func<T1, T2> f, Func<T2, T3> g)  // compositing function 
        {
            return (x) => g(f(x));
        }


    }
    #endregion Functions




    class City
    {
        public double x { set; get; }
        public double y { set; get; }

        public string name { get; set; }
        public override string ToString()
        {
            return $"({name})";
        }


    }

    class Chromosome
    {
        public List<City> route;
        public double fitness_score;

        public void displayRoute()
        {

            Console.Write("[ ");
            foreach (City city in route) { Console.Write(" " + city + ","); }
            Console.Write(" ]");
            Console.WriteLine();
            //return $"({foreach (City city in List<City> route ){Console.WriteLine(city + ",");}})"; // cant make it work with fstring
        }
    }

    class CrossoverCandidates
    {
        public List<(Chromosome, Chromosome)> candidates; // candidates for cross over 
    }
    class Generation
    {
        public ConcurrentBag<Chromosome> chrom_list = new ConcurrentBag<Chromosome>();
        public ConcurrentBag<Chromosome> elites = new ConcurrentBag<Chromosome>();
    }

    public class hyperparameters
    {
        public int popSize { get; set; }

        public int nGenerations { get; set; }

        public double mutationRate { get; set; }

        public double elitesNum { get; set; } // add elite number later , maybe percent of population 



    }

    public class options // used to decide type of operators used
    {
        public SelectionOperator selection;
        public MutationOperator mutOperator;
        public CrossoverOperator doublePointOperator;
        public ElitesNum nElites;
        public Fitness fit;

    }
    public enum Fitness
    {
        d   // just to add to make generation class , placeholder if there is different fitness functions
    }
    public enum SelectionOperator
    {
        RoulleteWheel,
        TournmentSelection  // add function later

    }
    public enum CrossoverOperator
    {
        doublePointOperator
    }
    public enum MutationOperator
    {
        Swap

    }

    public enum ElitesNum
    {
        two
    }

}