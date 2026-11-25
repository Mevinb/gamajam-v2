"""Locked story timing for the complete opening; captions remain live, accessible game UI."""
FPS=24
DURATION=80
SHOTS=[
 {'id':'01-rhythm','start':0,'end':5,'beat':'A living specimen answers a four-tap rhythm behind real glass.','eye':(.15,1.44,-2.10),'end_eye':(.05,1.42,-2.02),'target':(0,1.29,0),'lens':44},
 {'id':'02-petrov','start':5,'end':12,'beat':'Petrov approaches and physically taps the vessel four times.','eye':(-1.9,1.65,-2.9),'end_eye':(-1.5,1.62,-2.55),'target':(-.25,1.3,.12),'lens':38},
 {'id':'03-recognition','start':12,'end':19,'beat':'The specimen repeats the rhythm; Petrov recognizes the response.','eye':(-.15,1.64,1.45),'end_eye':(-.20,1.63,1.30),'target':(-.68,1.48,.08),'lens':55},
 {'id':'04-disagreement','start':19,'end':26,'beat':'Vale dismisses the response; Petrov turns to challenge him.','eye':(-1.7,1.65,.7),'end_eye':(-1.45,1.63,.72),'target':(.85,1.5,1.05),'lens':55},
 {'id':'05-two-bodies','start':26,'end':34,'beat':'One culture pinches into two synchronized, independently moving bodies.','eye':(-.4,1.45,-1.5),'end_eye':(.55,1.38,-1.45),'target':(-.02,1.29,0),'lens':52},
 {'id':'06-contact','start':34,'end':43,'beat':'A living filament touches the worker wrist; he recoils and collapses while Petrov intervenes.','eye':(.2,1.70,-3.4),'end_eye':(1.9,2.0,-3.4),'target':(.95,1.20,-.50),'end_target':(.85,.40,-1.1),'lens':36,'end_lens':38,'keys':[(34,(.2,1.70,-3.4),(.95,1.2,-.5),36),(36,(.6,1.8,-3.6),(.95,1.0,-.6),34),(38.5,(1.65,2.1,-3.6),(.95,.45,-1.0),32),(43,(1.9,2.0,-3.4),(.85,.40,-1.1),38)]},
 {'id':'07-restraint','start':43,'end':51,'beat':'Mechanical restraint closes; Petrov returns to the familiar rhythm.','eye':(-1.65,1.48,-1.55),'end_eye':(-1.30,1.44,-1.42),'target':(-.1,1.3,0),'lens':40},
 {'id':'08-interlock','start':51,'end':59,'beat':'Vale secretly releases a physical interlock; the hatch opens and the culture finds a floor drain.','eye':(1.85,1.28,-.90),'end_eye':(1.65,1.10,-1.20),'target':(.78,1.15,-.17),'end_target':(.1,.7,-.40),'lens':64,'end_lens':44},
 {'id':'09-conduit','start':59,'end':67,'beat':'The culture crawls through a lit corrugated service duct, leaving a wet trace.','eye':(0,-3.82,1.0),'end_eye':(0,-3.82,4.0),'target':(0,-3.85,5.1),'end_target':(0,-3.85,8.5),'lens':31},
 {'id':'10-outside','start':67,'end':75,'beat':'The creature squeezes through a real street grate into a wet, inhabited city alley.','eye':(0,.18,36.8),'end_eye':(.2,.55,36.4),'target':(0,.09,38.2),'end_target':(.3,.45,38.9),'lens':36},
 {'id':'11-next-beat','start':75,'end':80,'beat':'Ordinary people pass beyond the alley as the creature chooses its next movement; gameplay receives control afterward.','eye':(.3,.55,36.4),'end_eye':(1.0,1.4,35.0),'target':(.3,.45,39.0),'end_target':(0,1.25,46),'lens':36,'end_lens':30},
]
CAPTIONS=[
 (.5,'SPECIMEN 7 · MEMORY','Before faces, there was a rhythm.'),(5,'',''),(6,'DR. HANA PETROV','Four taps. Then we begin.'),
 (11,'',''),(16,'DR. HANA PETROV','You remembered.'),(19.5,'DR. VALE','It anticipated a stimulus.'),
 (22,'DR. HANA PETROV','It knew I was here.'),(26.8,'SPECIMEN 7 · MEMORY','Two places. The same remembering.'),(33,'',''),
 (34.8,'MAINTENANCE WORKER',"That wasn't me."),(37.4,'DR. HANA PETROV','End contact.'),
 (39,'SPECIMEN 7 · MEMORY',"A hand that wasn't mine. A heart that was."),(44,'DR. HANA PETROV','Seven. Stay with the rhythm.'),
 (50,'',''),(53,'DR. HANA PETROV','Who released the interlock?'),(56.5,'SPECIMEN 7 · MEMORY','Then the wall stopped.'),
 (59,'',''),(67.6,'SPECIMEN 7 · MEMORY','Outside, nobody knew the rhythm.'),(75.2,'SPECIMEN 7 · MEMORY','I could make the next beat.'),
]
TAPS=[1,1.3,1.6,2.3,6,6.3,6.6,7.3,12.5,12.8,13.1,13.8,14.5,14.8,15.1,15.8,16.5,16.8,17.1,17.8,45,45.3,45.6]

def frame(t):return round(t*FPS)+1
