/**
 * The four kinds of content this installation manages, spelled the way the backend spells
 * its own enum members - which is what every contract on both sides already does with role
 * and status, and the reason a second spelling here would be a second fact about one enum.
 *
 * A category belongs to exactly one kind, and the categories of one kind are a list of its
 * own: the four kinds are partitions of the domain, not levels of anything, so nothing here
 * compares one to another.
 */
export type ContentType = 'VideoStream' | 'AudioStream' | 'VideoOnDemand' | 'Series'
