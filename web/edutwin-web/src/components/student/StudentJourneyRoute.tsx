import { useState } from "react";

export interface JourneyTopic {
  id: string;
  name: string;
  mastery: number;
  evidenceCount: number;
}

const MASTERED_FROM = 75;
const isMastered = (topic: JourneyTopic) => topic.evidenceCount > 0 && topic.mastery >= MASTERED_FROM;
const ROUTE_HEIGHT = 96;

const pointX = (index: number, count: number) => ((index + 0.5) / count) * 1000;
const pointY = (index: number) => (index % 2 === 0 ? ROUTE_HEIGHT * 0.32 : ROUTE_HEIGHT * 0.68);

function routePath(lastIndex: number, count: number): string {
  let path = `M ${pointX(0, count)} ${pointY(0)}`;
  for (let i = 1; i <= lastIndex; i++) {
    const midX = (pointX(i - 1, count) + pointX(i, count)) / 2;
    path += ` C ${midX} ${pointY(i - 1)}, ${midX} ${pointY(i)}, ${pointX(i, count)} ${pointY(i)}`;
  }
  return path;
}

/** The route draws itself once per subject per tab, so a daily visit never waits for it. */
function useFirstVisit(subjectKey: string): boolean {
  const [firstVisit] = useState(() => {
    const key = `edutwin-journey-played:${subjectKey}`;
    try {
      if (sessionStorage.getItem(key)) return false;
      sessionStorage.setItem(key, "1");
    } catch {
      return false;
    }
    return true;
  });
  return firstVisit;
}

/** The curriculum as a road: stops already passed, where the student is, and what lies ahead. */
export function StudentJourneyRoute({ topics, subjectKey }: { topics: JourneyTopic[]; subjectKey: string }) {
  const animate = useFirstVisit(subjectKey);
  const count = topics.length;
  if (count === 0) return null;

  const firstOpen = topics.findIndex((topic) => !isMastered(topic));
  const currentIndex = firstOpen === -1 ? count - 1 : firstOpen;

  return (
    <div className={`overflow-x-auto ${animate ? "journey-animate" : ""}`}>
      <div style={{ minWidth: `${count * 7.5}rem` }}>
        <div className="relative" style={{ height: ROUTE_HEIGHT }}>
          <svg aria-hidden="true" className="absolute inset-0 h-full w-full" viewBox={`0 0 1000 ${ROUTE_HEIGHT}`} preserveAspectRatio="none" fill="none">
            {count > 1 && <path d={routePath(count - 1, count)} stroke="rgb(255 255 255 / 0.28)" strokeWidth="3" strokeDasharray="2 9" strokeLinecap="round" vectorEffect="non-scaling-stroke" />}
          </svg>
          {currentIndex > 0 && (
            <svg aria-hidden="true" className="journey-progress absolute inset-0 h-full w-full" viewBox={`0 0 1000 ${ROUTE_HEIGHT}`} preserveAspectRatio="none" fill="none">
              <path d={routePath(currentIndex, count)} stroke="white" strokeWidth="4" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
            </svg>
          )}

          {topics.map((topic, index) => {
            const done = isMastered(topic);
            const current = index === currentIndex && !done;
            return (
              <div
                key={topic.id}
                className="absolute -translate-x-1/2 -translate-y-1/2"
                style={{ left: `${(pointX(index, count) / 1000) * 100}%`, top: pointY(index) }}
              >
                <div className="journey-stop relative" style={{ animationDelay: `${Math.min(index, 8) * 70}ms` }}>
                  {current && <span aria-hidden="true" className="journey-pulse absolute inset-0 rounded-full bg-white" />}
                  <span
                    className={`relative flex items-center justify-center rounded-full ${
                      done
                        ? "h-8 w-8 bg-white text-[#5b3fd1]"
                        : current
                        ? "h-9 w-9 bg-white text-[#5b3fd1]"
                        : "h-7 w-7 border-2 border-white/50 bg-[#5b3fd1] text-white/80 dark:bg-[#3a2a8f]"
                    }`}
                  >
                    {done ? (
                      <svg aria-hidden="true" className="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><path d="m5 12 5 5 9-10" /></svg>
                    ) : (
                      <span className="text-xs font-bold tabular-nums">{index + 1}</span>
                    )}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        <ol className="grid" style={{ gridTemplateColumns: `repeat(${count}, minmax(0, 1fr))` }}>
          {topics.map((topic, index) => {
            const done = isMastered(topic);
            const current = index === currentIndex && !done;
            return (
              <li key={topic.id} className="px-2 text-center">
                <p className={`text-sm leading-snug ${current || done ? "font-semibold text-white" : "text-white/80"}`}>{topic.name}</p>
                <p className="mt-0.5 text-xs tabular-nums text-white/85">
                  {done ? `Đã vững · ${topic.mastery.toFixed(0)}%` : current ? `Bạn đang ở đây · ${topic.mastery.toFixed(0)}%` : topic.evidenceCount > 0 ? `${topic.mastery.toFixed(0)}%` : "Chưa học"}
                </p>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
