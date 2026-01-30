using Godot;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Transactions;

public partial class PlayerController : CharacterBody3D
{
	#region serialized variables
	[Export]public float speed = 10f;
	private float startSpeed;
	[Export]public float jumpHeight = 8f;
	[Export]bool isJumping = false;
	[Export]public float jumpDelay = 0.2f;
	[Export]private float delayTimer;
	[Export]public float coyoteTime = 0.15f;
	[Export]private float coyoteTimer;
	[Export]bool isCrouching = false;
	[Export]bool accMode1 = true;
	[Export]bool accMode2 = false;
	[Export]bool accMode3 = false;

	[Export] float gravityMulti = 5;
	float customGravity = 0;
	float hoverTimer = 0;

	[Export] public Camera3D cam;
	#endregion
	private AnimationPlayer anim;
	private Node3D point;
	private CollisionShape3D collider;
    private bool isCrouchAnimStarted = false;
	float timer = 0;
	float idleTimer = 0;

	private ShapeCast3D ceilingCheck;
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
				speed = startSpeed/2;
			}
		else {speed = startSpeed;}
		if(Input.IsActionPressed("walk")){isCrouching = true;}
		else if(!ceilingCheck.IsColliding()) {isCrouching = false;}
		if(idleTimer >= 5)
		{
			anim.Play("Idle");
		}
		//else{isCrouching = false;}
		Move(delta);
		Jump(delta);
		Rotate(delta);
		Crouch();
		MoveAndSlide();
	}
	void Move(double delta)
	{
		Vector3 velocity = Velocity;
		float xMove = Input.GetAxis("moveLeft","moveRight");
		float zMove = Input.GetAxis("moveUp","moveDown");
		Vector3 norm = new Vector3(xMove,0,zMove).Normalized();
		if(norm == Vector3.Zero)
		{
			idleTimer += (float)delta;
		}
		else{idleTimer = 0;}
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
		if(accMode3)
		{
			if(xMove != 0 || zMove != 0)
			{
				timer += (float)delta;
				velocity.X = Mathf.Lerp(velocity.X,norm.X*speed,(float)delta*timer/2);
				velocity.Z = Mathf.Lerp(velocity.Z,norm.Z*speed,(float)delta*timer);
			}
			else
			{
				timer = 0;
				velocity.X = Mathf.MoveToward(velocity.X,0,(float)delta*20);
				velocity.Z = Mathf.MoveToward(velocity.Z,0,(float)delta*20);
			}
		}
		Velocity = velocity;
	}
	void Rotate(double delta)
	{
		Vector3 velocity = Velocity;
		float xLook = Input.GetAxis("moveLeft","moveRight");
		float zLook = Input.GetAxis("moveUp","moveDown");
		Vector3 norm = new Vector3(xLook,0,zLook); // move direction
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
			hoverTimer += (float)delta;
			if(hoverTimer < 0.3f && Input.IsActionPressed("jump"))
			{
				customGravity = GetGravity().Y;
			}
			else if(hoverTimer > 0.3f && hoverTimer < 1f && Input.IsActionPressed("jump"))
			{
				customGravity = customGravity / 2;
			}
			else if ( hoverTimer > 1f && Input.IsActionPressed("jump"))
			{
				customGravity *= gravityMulti;
			}
			if(!Input.IsActionPressed("jump")){ customGravity *= gravityMulti;}
			velocity.Y +=  customGravity * (float)delta;
			coyoteTimer -= (float)delta;
			idleTimer = 0;
		}
		if(IsOnFloor()){coyoteTimer = coyoteTime; hoverTimer = 0;}
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
				anim.Play("Jump");
			}
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

