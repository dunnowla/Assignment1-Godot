using Godot;
using System;
using System.Diagnostics;
using System.IO.Pipes;
//using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Transactions;

public partial class PlayerController : CharacterBody3D
{
	#region variables

	//Movement
	[Export]public float speed = 10f;
	private float startSpeed;
	[Export]bool accMode1 = true;
	[Export]bool accMode2 = false;
	[Export]bool accMode3 = false;
	[Export] float sprintSpeed = 15f;

	//Jumping
	[Export]public float jumpHeight = 8f;
	[Export]bool isJumping = false;
	[Export]public float jumpDelay = 0.2f;
	[Export]private float delayTimer;
	[Export]public float coyoteTime = 0.15f;
	[Export]private float coyoteTimer;
	[Export] public float gravityMulti = 5f;
	[Export] public float hangTime = 1.5f;
	[Export] public float hangTimeGrav = 0.3f;
	private float customGravity = 0f;

	//Crouching
	[Export]bool isCrouching = false;
	private bool isCrouchAnimStarted = false;
	private ShapeCast3D ceilingCheck;

	//Camera
	[Export] public Camera3D cam;

	//Timers
	float timer = 0;
	float idleTimer = 0;

	//The rest
	private AnimationPlayer anim;
	private Node3D point;
	private CollisionShape3D collider;
	#endregion

    public override void _Ready()
    {
		startSpeed = speed;
        coyoteTimer = coyoteTime;
		point = GetNode<Node3D>("PivotPoint");
		collider = GetNode<CollisionShape3D>("TopCol");
		anim = GetNode<AnimationPlayer>("Animations");
		ceilingCheck = GetNode<ShapeCast3D>("CeilingCheck");
    }

	public override void _PhysicsProcess(double delta)
	{
		if(IsOnFloor() && isCrouching)
		{
			speed = startSpeed/2; // slows player down if crouching
		}
		else if(Input.IsActionPressed("sprint") && !isCrouching)
		{
			speed = sprintSpeed; // speeds player up if sprinting
		}
		else {speed = startSpeed;} // the normal walking speed
		if(Input.IsActionPressed("walk")){isCrouching = true;} 
		else if(!ceilingCheck.IsColliding()) {isCrouching = false;} // stops the crouch if the player has no obstacle over its head
		if(idleTimer >= 5)
		{
			anim.Play("Idle"); // Plays the idle animation if player dont move for 5 seconds
		}
		Move(delta);
		Jump(delta);
		Rotate(delta);
		Crouch();
		MoveAndSlide();
	}
	void Move(double delta)
	{
		Vector3 velocity = Velocity;
		// Gets all the input for moving the player
		float xMove = Input.GetAxis("moveLeft","moveRight");
		float zMove = Input.GetAxis("moveUp","moveDown");

		// Gets teh direction we want the player to move in
		Vector3 camForward = cam.GlobalTransform.Basis.Z;
		Vector3 camRight = cam.GlobalTransform.Basis.X;

		// Flattens the vectors so the character always moves forward
		camForward.Y = 0;
		camRight.Y = 0;
		// Normalizes the vectors to keep the player speed consistent
		camForward = camForward.Normalized();
		camRight = camRight.Normalized();

		// Calculates the final direction vector
		Vector3 norm = (camRight * xMove + camForward * zMove).Normalized();
		if(norm == Vector3.Zero)
		{
			idleTimer += (float)delta; // If the player is standing still it activates the idle timer
		}
		else{idleTimer = 0;}
		// Sets the acceleration mode
		if(Input.IsKeyPressed(Key.Key1))
		{
			accMode1 = true;
			accMode2 = false;
			accMode3 = false;
		}
		if(Input.IsKeyPressed(Key.Key2))
		{
			accMode1 = false;
			accMode2 = true;
			accMode3 = false;
		}
		if(Input.IsKeyPressed(Key.Key3))
		{
			accMode1 = false;
			accMode2 = false;
			accMode3 = true;
		}
		// Instant acceleration mode
		// Sets your speed to the max speed directly
		if(accMode1)
		{
			if(xMove != 0 || zMove != 0)
			{
				velocity.X = norm.X *speed;
				velocity.Z = norm.Z *speed;
			}
			else
			{
				velocity.X = 0;
				velocity.Z = 0;
			}
		}
		// Slow constant Acceleration
		if(accMode2)
		{
			if(xMove != 0 && zMove == 0)
			{
				velocity.X = Mathf.MoveToward(velocity.X,norm.X*speed,(float)delta*2);
				velocity.Z = Mathf.MoveToward(velocity.Z,0,(float)delta*10);
			}
			else if(zMove != 0 & xMove == 0)
			{
				velocity.Z = Mathf.MoveToward(velocity.Z,norm.Z*speed,(float)delta*2);
				velocity.X = Mathf.MoveToward(velocity.X,0,(float)delta*10);
			}
			else if(xMove != 0 && zMove != 0)
			{
				velocity.X = Mathf.MoveToward(velocity.X,norm.X*speed,(float)delta*2);
				velocity.Z = Mathf.MoveToward(velocity.Z,norm.Z*speed,(float)delta*2);
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X,0,(float)delta*10);
				velocity.Z = Mathf.MoveToward(velocity.Z,0,(float)delta*10);
			}
		}
		// Ease acceleration and constant deceleration
		if(accMode3)
		{
			if(xMove != 0 || zMove != 0)
			{
				velocity.X = Mathf.Lerp(velocity.X,norm.X*speed,5* (float)delta);
				velocity.Z = Mathf.Lerp(velocity.Z,norm.Z*speed,5* (float)delta);
			}
			else
			{
				timer = 0;
				velocity.X = Mathf.MoveToward(velocity.X,0,(float)delta*30);
				velocity.Z = Mathf.MoveToward(velocity.Z,0,(float)delta*30);
			}
		}
		Velocity = velocity;
	}
	void Rotate(double delta)
	{
		Vector3 velocity = Velocity;
		Vector3 norm = new Vector3(Velocity.X,0,Velocity.Z); // move direction
		if(norm != Vector3.Zero)
		{
			// The target rotation
			Basis targetBasis = Basis.LookingAt(norm,Vector3.Up);
			// Rotate only the mesh
			point.Basis = point.Basis.Slerp(targetBasis,12*(float)delta);
		}
		Velocity = velocity;
	}
	void Jump(double delta)
	{
		Vector3 velocity = Velocity;
		customGravity = GetGravity().Y;
		// Adds gravity in air
		if(!IsOnFloor())
		{
			float currentGrav = 1.0f;
			// If we are at the peak of our jump we hang lower gravity to "hang"
			if(Mathf.Abs(Velocity.Y) < hangTime)
			{
				currentGrav = hangTimeGrav;
			}
			else if(!Input.IsActionPressed("jump")){ currentGrav = gravityMulti;} // If we release the jump button the gravity increases
			velocity.Y +=  customGravity * currentGrav * (float)delta; // calculates the gravity
			coyoteTimer -= (float)delta; // starts the coyote timer
			idleTimer = 0;
		}
		if(IsOnFloor()){coyoteTimer = coyoteTime;} // resets the coyote times
		// Makes the player jump, also holy if statement
		if (Input.IsActionJustPressed("jump") && IsOnFloor() ||
		Input.IsActionJustPressed("jump") && !IsOnFloor() && coyoteTimer > 0 && isJumping == false)
		{
			isJumping = true;
		}
		if(isJumping)
		{
			delayTimer += (float)delta;
			if(anim.CurrentAnimation != "Jump")
			{
				anim.Play("Jump"); // Plays the jumping animation
			}
			// Makes the player jump if the jump delay is over
			if(delayTimer >= jumpDelay)
			{
				velocity.Y = jumpHeight;
				isJumping = false;
				delayTimer = 0;
			}
		}
		Velocity = velocity;
	}
	void Crouch()
	{
		// Plays the crouching animation and pushes the top collider into the bottom one to crouch
		if(isCrouching)
		{
			idleTimer = 0;
			if(!isCrouchAnimStarted)
			{
				anim.Play("Crouch");
				isCrouchAnimStarted = true;
				collider.Position = new Vector3(0,-0.5f,0);
			}
		}
		// Stops the crouching animation, resets the collider and resets the players speed
		else if (!isCrouching)
		{
			if(isCrouchAnimStarted)
			{
				speed = startSpeed;
				anim.Stop();
				collider.Position = new Vector3(0,0.5f,0);
				isCrouchAnimStarted = false;
			}
		}
	}
}

